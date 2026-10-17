# SignalR in this POC

SignalR keeps a connection open between browser and server so data can flow **both ways**:

- **Server to client:** the server pushes data the moment something happens, so the browser never polls. The Angular PnL screen updates by itself whenever new payments are saved (flat file, real-time API, Kafka or the UI popup).
- **Client to server:** the **Add Payment** popup calls a hub method (`AddPayment`) over the same connection to save a record.

## 1. Big picture

```
 CLIENT -> SERVER (invoke a hub method)
 Add Payment popup -> PaymentHubService.addPayment() -> PaymentHub.AddPayment() -> save to DB --+
                                                                                                |
 Other sources of new data                                                                      |
 Flat file in /Input        --+                                                                 |
 Kafka consumer             --+-> save to DB -------------------------------------------------+
                                                                                                v
                                                                        PaymentNotifier.PublishAsync()
 SERVER -> CLIENT (push)                                                                        | IHubContext<PaymentHub>
 App component refreshes table <- PaymentHubService.start() <- "ValidUpdated"/"InvalidUpdated" <-+
```

| Layer | File | Role |
|-------|------|------|
| Backend | [PaymentHub.cs](BNP_Usecase1/HUB/PaymentHub.cs) | The endpoint browsers connect to, and the `AddPayment` method they call |
| Backend | [PaymentNotifier.cs](BNP_Usecase1/HUB/PaymentNotifier.cs) | Helper that pushes messages to all connected browsers |
| Backend | [Program.cs](BNP_Usecase1/Program.cs) | Registers SignalR, CORS and maps the hub URL |
| Frontend | [payment-hub.service.ts](BNP_Usecase1/ClientApp/src/app/services/payment-hub.service.ts) | Opens the connection, listens for pushes and sends `AddPayment` |
| Frontend | [app.ts](BNP_Usecase1/ClientApp/src/app/app.ts) / [app.html](BNP_Usecase1/ClientApp/src/app/app.html) | Add Payment popup, and updating the table |

---

## 2. Backend

### 2.1 `PaymentHub`

```csharp
public class PaymentHub(IPaymentBusiness business, PaymentNotifier notifier) : Hub
{
    public async Task<FeedResult> AddPayment(List<PaymentEntity> payments)
    {
        if (payments == null || payments.Count == 0)
            throw new HubException("At least one record is required.");

        var result = business.InsertBatch(payments, PaymentConstants.External);
        await notifier.PublishAsync(business);
        return result;
    }
}
```

**What it is:** a *hub* is the SignalR "room" that clients connect to. Inheriting from `Hub` gives it connection handling (connect, disconnect, groups, and so on). Public methods on the hub can be **called by the browser**.

| Part | Explanation |
|------|-------------|
| `AddPayment(...)` | **Client to server.** The browser calls it by name with `connection.invoke('AddPayment', payments)`. The name must match. |
| `HubException` | The safe way to reject a call. The message reaches the browser as a failed promise. |
| `PaymentEntity` | The source data the browser sends: `SourceSystem`, `AccountNumber`, `PnLAmount`. The same type is used for flat-file lines and Kafka messages. |
| `PaymentConstants.External` | The source type passed to `InsertBatch`, which marks records added from the UI, next to `File` and `Messaging`. |
| `InsertBatch` | Turns each `PaymentEntity` into a `PaymentDTO` (adds source type, file name and status), then saves. Same path as the file feed (amount `0` is saved as Invalid). |
| `PublishAsync` | Pushes the updated lists back to **all** browsers (server to client, section 2.2). |
| Return value | The `FeedResult` (total, valid, invalid, persisted) goes back to the caller as the result of `invoke`. |

The hub gets `IPaymentBusiness` through its constructor. SignalR creates a new hub instance and a DI scope for every call, so a scoped service is fine here.

**Real-time example:** think of the hub as a two-way radio. The user presses talk (`AddPayment`), the server saves the record, and then broadcasts the new lists to everyone listening.

### 2.2 `PaymentNotifier`

```csharp
public class PaymentNotifier(IHubContext<PaymentHub> hub)
{
    public async Task PublishAsync(IPaymentBusiness business)
    {
        await hub.Clients.All.SendAsync("ValidUpdated",   business.GetByStatus(PaymentConstants.Valid));
        await hub.Clients.All.SendAsync("InvalidUpdated", business.GetByStatus(PaymentConstants.Invalid));
    }
}
```

| Part | Explanation |
|------|-------------|
| `IHubContext<PaymentHub>` | Lets code **outside** the hub (controllers, background services) send messages through it. A hub instance only lives for one call, so you cannot keep one around; the context is the supported way. |
| `PublishAsync(business)` | The one public method. Call it **after** new records are saved. |
| `hub.Clients.All` | Targets every connected browser. Alternatives are `Clients.Client(id)`, `Clients.Group("FX")`, `Clients.User(id)`. |
| `SendAsync("ValidUpdated", data)` | Sends a message named `ValidUpdated`, with the current valid list as the payload (serialized to JSON). The name is a string that the frontend must match **exactly**. |
| `GetByStatus(Valid/Invalid)` | Reads the same lists that `GET /api/payment/valid` and `/invalid` return, so push and pull always agree. |

It is registered as a singleton in `Program.cs`: `builder.Services.AddSingleton<PaymentNotifier>();`

**Who calls it** (all three paths end up on the same screen):

| Trigger | Where |
|---------|-------|
| Add Payment popup (`AddPayment` hub method) | [PaymentHub.cs:17](BNP_Usecase1/HUB/PaymentHub.cs#L17) |
| A `.txt` file dropped in the input folder | [FlatFileBackgroundService.cs:72](BNP_Usecase1/FlatFileBackgroundService.cs#L72) |
| A Kafka message consumed | [KafkaConsumerService.cs:77](BNP_Usecase1/Messaging/KafkaConsumerService.cs#L77) |

**Real-time example:** an operations user has the *Valid* tab open. A colleague saves 3 payments through the Add Payment popup. The hub saves them, calls `PublishAsync`, and a moment later the 3 new rows appear on the user's screen with no refresh.

### 2.3 `Program.cs` wiring

| Line | Purpose |
|------|---------|
| `builder.Services.AddSignalR();` | Turns SignalR on. |
| `builder.Services.AddSingleton<PaymentNotifier>();` | Makes the notifier injectable. |
| `AddCors(... WithOrigins(angularUrl) ... AllowCredentials())` | `angularUrl` comes from `AngularClient:Url` in `appsettings*.json` (`http://localhost:4200`). Angular runs on port 4200 and the API on another port, so the browser needs CORS permission. SignalR requires `AllowCredentials()` and a **specific origin**; `AllowAnyOrigin()` does not work. `POST` is allowed because the SignalR `negotiate` handshake is a POST. Saving from the popup does not need extra CORS rules, because it travels over the SignalR connection, not a separate REST call. |
| `endpoints.MapHub<PaymentHub>("/hubs/payment");` | Publishes the hub at this URL. It must match the URL in the Angular service. |

---

## 3. Frontend

### 3.1 `PaymentHubService`

```ts
@Injectable({ providedIn: 'root' })
export class PaymentHubService {
  private readonly connection = new HubConnectionBuilder()
    .withUrl(`${environment.apiBaseUrl}/hubs/payment`)
    .withAutomaticReconnect()
    .build();

  start(onValid, onInvalid): void {
    this.connection.on('ValidUpdated', onValid);
    this.connection.on('InvalidUpdated', onInvalid);
    this.connection.start().catch(err => console.error('SignalR connection failed', err));
  }

  addPayment(payments: NewPayment[]): Promise<unknown> {
    return this.connection.invoke('AddPayment', payments);
  }
}
```

| Part | Explanation |
|------|-------------|
| `providedIn: 'root'` | One shared instance for the whole app, so there is only **one** connection. |
| `HubConnectionBuilder` | Builder from `@microsoft/signalr` that configures the connection. |
| `.withUrl(.../hubs/payment)` | Address of the hub. `apiBaseUrl` comes from the environment file (`http://localhost:42688` in development). |
| `.withAutomaticReconnect()` | If the network drops or the server restarts, it retries automatically (about 0s, 2s, 10s, 30s). Without it the screen would silently stop updating. |
| `.build()` | Creates the connection object. It is **not connected yet**. |
| `start(onValid, onInvalid)` | **Server to client.** The component passes in two callbacks to say what to do with each kind of update. |
| `addPayment(payments)` | **Client to server.** Calls the hub method `AddPayment` with `invoke`. The returned promise resolves with the server's `FeedResult`, or rejects if the server throws a `HubException` or the connection is not open. |
| `connection.on('ValidUpdated', onValid)` | Registers a listener. When the server sends `ValidUpdated`, `onValid(data)` runs. The name must match `SendAsync("ValidUpdated", ...)`. |
| `connection.on('InvalidUpdated', onInvalid)` | Same for the invalid list. |
| `connection.start()` | Opens the connection: negotiate, then WebSocket (falling back to SSE or long polling). Returns a promise, so a failure is logged instead of crashing the app. |

Handlers are registered **before** `start()` on purpose, so no message sent right after connecting is missed.

### 3.2 How the component uses it ([app.ts](BNP_Usecase1/ClientApp/src/app/app.ts))

```ts
ngOnInit(): void {
  this.load();   // first load through normal HTTP
  this.hub.start(
    data => { if (this.activeTab === 'valid')   this.records = data; },
    data => { if (this.activeTab === 'invalid') this.records = data; });
}
```

- `load()` fetches the initial data with a normal HTTP GET. SignalR only delivers **changes after** the connection is open.
- Each callback replaces the table only if the user is on the matching tab. If the user is viewing *Valid* and an `InvalidUpdated` arrives, it is ignored, because the invalid list is not on screen.

The **Add Payment** popup (client to server):

```ts
submitAdd(): void {
  // ... check that all fields are filled ...
  this.hub.addPayment([{ sourceSystem, accountNumber, pnLAmount }])
    .then(() => { this.saving = false; this.showAdd = false; })
    .catch(() => { this.saving = false; this.addError = 'Unable to save the record.'; });
}
```

- The popup closes when the server call succeeds, and shows an error when it fails.
- The table is **not** reloaded here. The server pushes `ValidUpdated` and `InvalidUpdated` after saving, and the callbacks above update the table.
- The form markup is in [app.html](BNP_Usecase1/ClientApp/src/app/app.html) (the `@if (showAdd)` block).

---

## 4. End-to-end walkthrough

1. The user opens `http://localhost:4200`. `App.ngOnInit` loads the Valid list over HTTP and calls `hub.start(...)`.
2. The browser sends `POST /hubs/payment/negotiate`, then upgrades to a WebSocket.
3. The user clicks **Add Payment** and enters Source System `SRC1`, Account Number `1001`, PnL Amount `2500`, then clicks Save.
4. **Client to server:** `hub.addPayment(...)` calls `connection.invoke('AddPayment', [...])` on the open connection.
5. `PaymentHub.AddPayment` sets `SourceType = External`, saves the record (valid, because the amount is not 0) and calls `notifier.PublishAsync(business)`.
6. **Server to client:** the server sends `ValidUpdated` (all valid rows) and `InvalidUpdated` (all invalid rows) to every connected browser, including this one.
7. In the browser, `onValid` runs. The user is on the Valid tab, so the table updates immediately and the new row appears. `onInvalid` runs and is ignored. The `invoke` promise resolves and the popup closes.
8. A colleague with the page open in another browser sees the same new row, with no action on their side.
9. If the user had entered `0` as the amount, the record would be saved as Invalid and appear on the *Invalid* tab.

The server to client part is the same when a file lands in the input folder (`FlatFileBackgroundService`), or when a Kafka message is consumed (`KafkaConsumerService`). Only step 4 differs.

---

## 5. How to test it

1. Start the API and Angular (`npm start` in `ClientApp`) and open the page.
2. Open DevTools, **Network**, filter **WS**. You should see a `payment` connection with messages flowing.
3. Click **Add Payment**, fill the form and Save. The WS tab should show an `AddPayment` frame going out and `ValidUpdated` and `InvalidUpdated` frames coming back.
4. The table should update without a refresh. Open the page in a second browser window to see it update there too.
5. To test the server to client path alone, send a record to `POST /api/payment/publish` from [BNP_Usecase1.http](BNP_Usecase1/BNP_Usecase1.http) or Swagger (Kafka must be running).

## 6. Troubleshooting

| Symptom | Likely cause |
|---------|--------------|
| CORS error on `/negotiate` | Origin is not `http://localhost:4200`, or `AllowCredentials()` is missing. |
| 404 on `/hubs/payment` | The `MapHub` path and the Angular `withUrl` path do not match. |
| Connected but table does not change | Event name is different on the server and the client (case-sensitive), or the user is on the other tab. |
| Save shows "Unable to save the record." | The connection is not open yet (for example the API is down or still starting), the hub method name does not match `AddPayment`, or the server threw. Check the browser console and the API log. |
| Updates stop after the server restarts | Check that `.withAutomaticReconnect()` is still there. After the reconnect, data sent while offline is lost, so call `load()` again if that matters. |

## 7. Limits of this POC

- **Broadcast to everyone:** `Clients.All` sends to every user. Use groups or users if data should be per desk or per person.
- **Full list on every update:** the whole valid and invalid lists are sent each time. Fine for a POC; for large data, send only the new rows.
- **No authentication** on the hub, so any connected client can call `AddPayment`.
- **Only basic validation** on `AddPayment`: an empty list is rejected and the rest follows the normal business rules.
- **Scale-out:** with more than one server instance you need a backplane (Redis or Azure SignalR) so every instance can reach every client.
