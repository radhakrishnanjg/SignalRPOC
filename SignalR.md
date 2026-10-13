# SignalR in this POC

SignalR gives the server a way to **push** data to the browser the moment something happens, so the browser never has to poll. In this project the Angular PnL screen updates by itself whenever new payments are saved, whether they came from a flat file, the real-time API or Kafka.

## 1. Big picture

```
 Source of new data                    Backend (ASP.NET Core)                         Frontend (Angular)
 ──────────────────                    ──────────────────────                         ──────────────────
 Flat file in /Input      ─┐
 POST /api/payment/realtime ├─► save to DB ─► PaymentNotifier.PublishAsync()
 Kafka consumer           ─┘                         │
                                                     │ IHubContext<PaymentHub>
                                                     ▼
                                          PaymentHub  (/hubs/payment)
                                                     │  WebSocket
                                                     ▼
                                    "ValidUpdated" / "InvalidUpdated"  ──►  PaymentHubService.start()
                                                                                    │
                                                                                    ▼
                                                                          App component refreshes table
```

| Layer | File | Role |
|-------|------|------|
| Backend | [PaymentHub.cs](BNP_Usecase1/HUB/PaymentHub.cs) | The endpoint browsers connect to |
| Backend | [PaymentNotifier.cs](BNP_Usecase1/HUB/PaymentNotifier.cs) | Helper that pushes messages to all connected browsers |
| Backend | [Program.cs](BNP_Usecase1/Program.cs) | Registers SignalR, CORS and maps the hub URL |
| Frontend | [payment-hub.service.ts](BNP_Usecase1/ClientApp/src/app/services/payment-hub.service.ts) | Opens the connection and listens for messages |
| Frontend | [app.ts](BNP_Usecase1/ClientApp/src/app/app.ts) | Uses the service and updates the table |

---

## 2. Backend

### 2.1 `PaymentHub`

```csharp
public class PaymentHub : Hub
{
}
```

**What it is:** a *hub* is the SignalR "room" that clients connect to. Inheriting from `Hub` gives it connection handling (connect, disconnect, groups, and so on).

**Why it is empty:** in this POC the traffic is **one way**, server to browser. The browser never calls a hub method, so no methods are needed. Pushing is done from `PaymentNotifier`.

**Real-time example:** think of the hub as a radio tower. The browser tunes in at `/hubs/payment`, and the tower (server) broadcasts whenever there is news. The listener never calls the tower.

**If you later wanted the browser to call the server**, you would add a method here:

```csharp
public class PaymentHub : Hub
{
    // Browser calls: connection.invoke("JoinDesk", "FX")
    public Task JoinDesk(string desk) => Groups.AddToGroupAsync(Context.ConnectionId, desk);
}
```

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
| `POST /api/payment/realtime` | [PaymentController.cs:31](BNP_Usecase1/Controllers/PaymentController.cs#L31) |
| A `.txt` file dropped in the input folder | [FlatFileBackgroundService.cs:72](BNP_Usecase1/FlatFileBackgroundService.cs#L72) |
| A Kafka message consumed | [KafkaConsumerService.cs:77](BNP_Usecase1/Messaging/KafkaConsumerService.cs#L77) |

**Real-time example:** an operations user has the *Valid* tab open. A source system posts 3 payments to `/api/payment/realtime`. The controller saves them, calls `PublishAsync`, and a moment later the 3 new rows appear on the user's screen with no refresh.

### 2.3 `Program.cs` wiring

| Line | Purpose |
|------|---------|
| `builder.Services.AddSignalR();` | Turns SignalR on. |
| `builder.Services.AddSingleton<PaymentNotifier>();` | Makes the notifier injectable. |
| `AddCors(... WithOrigins("http://localhost:4200") ... AllowCredentials())` | Angular runs on port 4200 and the API on another port, so the browser needs CORS permission. SignalR requires `AllowCredentials()` and a **specific origin**; `AllowAnyOrigin()` does not work. `POST` is allowed because the SignalR `negotiate` handshake is a POST. |
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
}
```

| Part | Explanation |
|------|-------------|
| `providedIn: 'root'` | One shared instance for the whole app, so there is only **one** connection. |
| `HubConnectionBuilder` | Builder from `@microsoft/signalr` that configures the connection. |
| `.withUrl(.../hubs/payment)` | Address of the hub. `apiBaseUrl` comes from the environment file (`http://localhost:42688` in development). |
| `.withAutomaticReconnect()` | If the network drops or the server restarts, it retries automatically (about 0s, 2s, 10s, 30s). Without it the screen would silently stop updating. |
| `.build()` | Creates the connection object. It is **not connected yet**. |
| `start(onValid, onInvalid)` | The only public method. The component passes in two callbacks to say what to do with each kind of update. |
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

---

## 4. End-to-end walkthrough

1. The user opens `http://localhost:4200`. `App.ngOnInit` loads the Valid list over HTTP and calls `hub.start(...)`.
2. The browser sends `POST /hubs/payment/negotiate`, then upgrades to a WebSocket.
3. Another system posts this to `POST /api/payment/realtime`:
   ```json
   [
     { "sourceSystem": "SRC1", "accountNumber": "ACC100", "pnLAmount": 2500.50 },
     { "sourceSystem": "SRC1", "accountNumber": "",       "pnLAmount": 100 }
   ]
   ```
4. `PaymentController.Realtime` saves both. One is valid and one is invalid because the account number is missing.
5. It calls `notifier.PublishAsync(business)`. The server sends `ValidUpdated` (all valid rows) and `InvalidUpdated` (all invalid rows) to every connected browser.
6. In the browser, `onValid` runs. The user is on the Valid tab, so the table updates immediately. `onInvalid` runs and is ignored.
7. The user clicks the *Invalid* tab, which calls `load()` and shows the invalid row.

The same happens when a file lands in the input folder (step 3 replaced by `FlatFileBackgroundService`) or when a Kafka message is consumed (`KafkaConsumerService`).

---

## 5. How to test it

1. Start the API and Angular (`npm start` in `ClientApp`) and open the page.
2. Open DevTools, **Network**, filter **WS**. You should see a `payment` connection with messages flowing.
3. Send a record from [BNP_Usecase1.http](BNP_Usecase1/BNP_Usecase1.http) or Swagger to `POST /api/payment/realtime`.
4. The table should update without a refresh, and the WS tab should show `ValidUpdated` and `InvalidUpdated` frames.

## 6. Troubleshooting

| Symptom | Likely cause |
|---------|--------------|
| CORS error on `/negotiate` | Origin is not `http://localhost:4200`, or `AllowCredentials()` is missing. |
| 404 on `/hubs/payment` | The `MapHub` path and the Angular `withUrl` path do not match. |
| Connected but table does not change | Event name is different on the server and the client (case-sensitive), or the user is on the other tab. |
| Updates stop after the server restarts | Check that `.withAutomaticReconnect()` is still there. After the reconnect, data sent while offline is lost, so call `load()` again if that matters. |

## 7. Limits of this POC

- **Broadcast to everyone:** `Clients.All` sends to every user. Use groups or users if data should be per desk or per person.
- **Full list on every update:** the whole valid and invalid lists are sent each time. Fine for a POC; for large data, send only the new rows.
- **No authentication** on the hub.
- **Scale-out:** with more than one server instance you need a backplane (Redis or Azure SignalR) so every instance can reach every client.
