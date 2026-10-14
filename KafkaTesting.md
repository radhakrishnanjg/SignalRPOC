# Kafka Testing Guide

End-to-end test of the real-time feed:

```
Swagger/PowerShell → POST /api/payment/publish → PaymentProducer
   → Kafka topic pnl-realtime (localhost:9092)
   → KafkaConsumerService (group pnl-consumer) → validate + SQL save → SignalR → Angular UI
```

Ports used below: API `42688` (IIS Express profile, matches the Angular environment file), Angular `4200`, Kafka `9092`.

## 0. Prerequisites

- Kafka 4.x extracted to `C:\kafka`, with `log.dirs=C:/kafka/kraft-combined-logs` in `config\server.properties`.
- JDK 17 installed. Every Kafka command window needs it:
  ```cmd
  set "JAVA_HOME=C:\Program Files\Eclipse Adoptium\jdk-17.x.x-hotspot"
  set "PATH=%JAVA_HOME%\bin;%PATH%"
  ```
  (use your real JDK 17 folder name)
- SQL Server `.\sqlexpress` with database `BnBUsecase`, the table `Payment.TransactionDetails` and the stored procedure `Payment.usp_TransactionDetail_Insert` (scripts in `BNP_Usecase1\DBScripts`).
- Angular dev server must call the API you actually run: `BNP_Usecase1\ClientApp\src\environments\environment.development.ts` has `apiBaseUrl: 'http://localhost:42688'`. Keep the API and this value on the same port.

> The `main ERROR Reconfiguration failed: No configuration found` line printed by Kafka tools is a harmless log4j warning. The `TimeoutException` at the end of `--timeout-ms` runs is also expected.

## 1. Start Kafka (first time only: format)

```cmd
cd C:\kafka
.\bin\windows\kafka-storage.bat random-uuid
.\bin\windows\kafka-storage.bat format --standalone -t <UUID> -c .\config\server.properties
```

Start the broker (leave this window open):

```cmd
.\bin\windows\kafka-server-start.bat .\config\server.properties
```

Pass: the log shows `Kafka Server started`.

## 2. Verify the broker and topic

New window (set JAVA_HOME again), then:

```cmd
cd C:\kafka
.\bin\windows\kafka-topics.bat --bootstrap-server localhost:9092 --list
```

Pass: `pnl-realtime` is listed. If it is not, create it:

```cmd
.\bin\windows\kafka-topics.bat --bootstrap-server localhost:9092 --create --topic pnl-realtime --partitions 1 --replication-factor 1
```

("Topic already exists" is fine.)

Quick port check: `Test-NetConnection localhost -Port 9092` → `TcpTestSucceeded : True`.

## 3. Start the API (one instance only)

In Visual Studio pick the **IIS Express** profile and run (F5). It serves http://localhost:42688 .

Pass: the API output shows `Kafka consumer listening on topic pnl-realtime`.
Under IIS Express the app (and its consumer) only starts on the first request, so open http://localhost:42688/swagger once after starting.
Run only one API instance: two instances in group `pnl-consumer` split the messages between them.

## 4. Start Angular

```cmd
cd "E:\Working Folder\POC\SignalRPOC\BNP_Usecase1\ClientApp"
npm start
```

Open http://localhost:4200 and keep it visible.

## 5. Test A - valid record (the main test)

PowerShell:

```powershell
$body = '[{"sourceSystem":"SYS1","accountNumber":1001,"pnLAmount":2500}]'
Invoke-RestMethod -Method Post -Uri http://localhost:42688/api/payment/publish -ContentType "application/json" -Body $body
```

(or use Swagger at http://localhost:42688/swagger → `POST /api/Payment/publish`; do not leave the default `"string", 0, 0` values.)

| Check | Expected |
|---|---|
| HTTP response | `202 Accepted` (message is on Kafka) |
| API console | no `Kafka consume/save failed` |
| Angular UI | new row appears live, status **Valid** |
| SQL | row exists (see section 8) |

## 6. Test B - invalid record

`PnLAmount = 0` is saved as **Invalid**:

```powershell
$body = '[{"sourceSystem":"SYS2","accountNumber":2002,"pnLAmount":0}]'
Invoke-RestMethod -Method Post -Uri http://localhost:42688/api/payment/publish -ContentType "application/json" -Body $body
```

Expected: row appears in the Invalid list, status **Invalid**.

## 7. Test C - several records at once

```powershell
$body = '[{"sourceSystem":"SYS1","accountNumber":3001,"pnLAmount":100},{"sourceSystem":"SYS1","accountNumber":3002,"pnLAmount":-50},{"sourceSystem":"SYS3","accountNumber":3003,"pnLAmount":0}]'
Invoke-RestMethod -Method Post -Uri http://localhost:42688/api/payment/publish -ContentType "application/json" -Body $body
```

Expected: three Kafka messages, three new rows (2 Valid, 1 Invalid).

## 8. Inspect Kafka directly

Messages on the topic:

```cmd
.\bin\windows\kafka-console-consumer.bat --bootstrap-server localhost:9092 --topic pnl-realtime --from-beginning --timeout-ms 5000
```

Consumer group state:

```cmd
.\bin\windows\kafka-consumer-groups.bat --bootstrap-server localhost:9092 --list
.\bin\windows\kafka-consumer-groups.bat --bootstrap-server localhost:9092 --describe --group pnl-consumer
```

Pass: `pnl-consumer` is listed, and `LAG` is `0`. `CURRENT-OFFSET` equals `LOG-END-OFFSET` once everything is saved.

Database:

```sql
SELECT TOP 20 * FROM Payment.TransactionDetails ORDER BY 1 DESC;
```

Pass: the rows from tests A, B and C are present (SourceType = `Messaging`; file feed rows are `File`, rows added from the UI popup are `External`).

## 9. Test D - Kafka holds messages while the API is down

1. Stop the API (Ctrl+C).
2. Send messages with the console producer, one JSON object per line, then Ctrl+C:
   ```cmd
   .\bin\windows\kafka-console-producer.bat --bootstrap-server localhost:9092 --topic pnl-realtime
   >{"SourceSystem":"SYS4","AccountNumber":4001,"PnLAmount":750}
   >{"SourceSystem":"SYS4","AccountNumber":4002,"PnLAmount":0}
   ```
3. Run the `--describe` command: `LAG` is `2`.
4. Start the API again. The consumer catches up, the 2 rows are saved and appear in the UI, and `LAG` returns to `0`.

## 10. Test E - API behaviour when Kafka is down

1. Stop the Kafka broker (Ctrl+C in its window).
2. Call `publish`. Expected: `500` after about 5 seconds (producer timeout).
3. The API itself stays up; the consumer logs errors and keeps retrying.
4. Start Kafka again; the consumer reconnects by itself.

## Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| `UnsupportedClassVersionError` | Kafka window is using Java 8. Set `JAVA_HOME` to JDK 17 in that window. |
| `No readable meta.properties files found` | Storage not formatted. Run the `format` command (section 1). |
| `config\kraft\server.properties` not found | Kafka 4.x: use `config\server.properties`. |
| `202` but nothing in DB or UI, group not listed | Consumer is not running or not connected. Check API console for `Kafka consumer listening...`. Under IIS Express the consumer only starts after the first request (open `/swagger` once) and stops when IIS recycles the app; keep only one instance running. |
| `Kafka consume/save failed` in API log | Read the exception. Most often the SQL connection string (`Trusted_Connection=True` together with `User ID/Password` is contradictory) or the stored procedure missing in `BnBUsecase`. |
| Rows in DB but the UI does not update | Angular `apiBaseUrl` points to a different API instance/port than the one running (see section 0). |
| Swagger default body saved as `string / 0 / 0` | Expected: it is a valid message with amount 0 (Invalid). Use real values. |
| Rows split between two API instances | Two instances share group `pnl-consumer`. Run only one. |
