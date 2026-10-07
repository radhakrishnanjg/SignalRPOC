# BNP Usecase 1 – PnL Capture, Validation and Reporting

Captures PnL records from a start-of-day flat file feed, flags records with a PnL amount of `0` as **Invalid** (all others **Valid**), stores everything in SQL Server and reports Valid / Invalid data in an Angular UI.
Real-time messaging (queue + live UI stream) is planned for a later phase.

## Tech Stack

### Backend
| Area | Technology | Version |
|---|---|---|
| Runtime / Framework | .NET / ASP.NET Core Web API | 8.0 (C# 12) |
| Architecture | Clean architecture: Entities → Repository → Services → API | – |
| Data access | Microsoft.Data.SqlClient (stored procedures, JSON insert via `OPENJSON`) | 5.2.2 |
| Background job | `BackgroundService` + Cronos (cron scheduling) | Cronos 0.13.0 |
| SPA hosting | Microsoft.AspNetCore.SpaServices.Extensions | 8.0.x |
| API docs | Swashbuckle.AspNetCore (Swagger) | 6.6.2 |

### Frontend (`BNP_Usecase1/ClientApp`)
| Area | Technology | Version |
|---|---|---|
| Framework | Angular (standalone components) | 20.3 |
| Language | TypeScript | 5.9 |
| UI | Bootstrap (CSS only) | 5.3 |
| HTTP / reactive | Angular HttpClient, RxJS | RxJS 7.8 |
| Live updates | @microsoft/signalr | 10.x |

### Messaging
| Area | Technology | Version |
|---|---|---|
| Message queue | Apache Kafka (Docker, KRaft single node) | 3.8.0 |
| .NET client | Confluent.Kafka | 2.15.1 |
| Tooling | Angular CLI, Node.js, npm | CLI 20.3 / Node 24 / npm 11 |

### Database
| Area | Technology |
|---|---|
| Database | SQL Server (SQL Express, database `BnBUsecase`, schema `Payment`) |
| Scripts | `BNP_Usecase1/DBScripts` – table `TransactionDetails` and stored procedures `usp_TransactionDetail_Insert`, `usp_TransactionDetail_GetByStatus`, `usp_TransactionDetail_FeedExist` (each with a rollback script) |

## Solution Layout
```
BNB.UsecaseEntities     DTOs, constants, validation rule
BNB.UsecaseRepository   SQL Server access via stored procedures
BNB.UsecaseServices     Business logic (feed parsing, validation, insert)
BNP_Usecase1            Web API, flat-file background service, DB scripts, ClientApp (Angular)
```

## Configuration (`appsettings*.json` → `ConnectionStrings`)
- `PaymentConnection` – SQL Server connection string
- `FlatFilePath` – folder watched for `*.txt` feed files (processed files move to `Processed`)
- `FlatFileCronExpression` – schedule for the file job (default `*/5 * * * *`)

Feed file format: `SourceSystem`, `AccountNumber`, `PnLAmount`, separated by tab or comma (header row optional).

## Running
1. Run the scripts in `BNP_Usecase1/DBScripts` on the `BnBUsecase` database.
2. Frontend (development): `cd BNP_Usecase1/ClientApp && npm install && npm start`
3. Backend: run `BNP_Usecase1` (`dotnet run`), then open `http://localhost:5293/` (Swagger at `/swagger`).
4. Production: `npm run build` in `ClientApp`; the API serves the output from `ClientApp/dist/ClientApp/browser`.

## API
| Method | Route | Purpose |
|---|---|---|
| GET | `/api/payment/{status}` | PnL report by status: `valid` or `invalid` (case-insensitive; anything else returns 400) |
| GET | `/api/payment` | All records |
| POST | `/api/payment/realtime` | Real-time records (JSON array) |

Angular `PaymentService` has a single method, `getPnl(status)`, which calls `GET /api/payment/{status}`.

## Real-time Feed (Kafka)
Flow: source system → **producer** → Kafka topic `pnl-realtime` → **consumer** (`Messaging/KafkaConsumerService.cs`, background service) → validate + save (same `InsertBatch` as the file feed) → SignalR push to the UI.
- Package: `Confluent.Kafka` 2.15.1. Config: `Kafka:BootstrapServers`, `Kafka:Topic`, `Kafka:GroupId` in `appsettings.json`.
- Broker: `docker-compose.yml` in the root runs a single-node Kafka (`apache/kafka:3.8.0`) on `localhost:9092`. Start with `docker compose up -d` (Docker Desktop must be running).
- `POST /api/payment/publish` is a test producer (JSON array of `sourceSystem`, `accountNumber`, `pnLAmount`): it puts records on the topic and returns 202; the consumer saves them. Records are stored with `SourceType = Realtime`.
- `POST /api/payment/realtime` still saves directly without Kafka.
- The app starts normally if Kafka is down (consumer logs errors and keeps retrying); `publish` then returns 500 after ~5 s.

## Error Handling
- `BNP_Usecase1/Middleware/AddGlobalExceptionMiddleware.cs` holds a `GlobalExceptionHandler` (.NET 8 `IExceptionHandler`), registered with `AddGlobalExceptionMiddleware()` and enabled with `UseGlobalExceptionMiddleware()` in `Program.cs`.
- Repository, business and controller code does not use try/catch; it just throws. The handler returns a JSON `ProblemDetails` response: `ArgumentException` / `FormatException` → 400 with the message, anything else → 500 with a generic message (full error is logged).
- The flat-file background service keeps its own try/catch, because it runs outside an HTTP request and the handler can't see it.

## Live Updates (SignalR)
- Hub: `/hubs/payment` (`BNP_Usecase1/HUB/PaymentHub.cs`, package `Microsoft.AspNetCore.SignalR`, built into .NET 8). Client package: `@microsoft/signalr`.
- After new records are saved (flat-file job or `POST /api/payment/realtime`), `PaymentNotifier.PublishAsync` pushes `ValidUpdated` and `InvalidUpdated` messages carrying the same lists as `GET /api/payment/valid` and `/invalid`.
- The page replaces the table when the pushed list matches the tab being viewed.
- Records changed directly in the database are not detected (no polling by design).

## Environments (Angular)
`src/environments/environment.development.ts` → `apiBaseUrl: http://localhost:42688` (used by `ng serve`); `environment.ts` → `apiBaseUrl: ''` (production, same origin). CORS in `Program.cs` allows `http://localhost:4200`.
