using System.Text.Json;
using BNB.UsecaseEntities;
using BNB.UsecaseServices.Interfaces;
using BNP_Usecase1.Hubs;
using Confluent.Kafka;

namespace BNP_Usecase1.Messaging
{
    /// <summary>
    /// Background Kafka consumer that saves each message and commits it only after the save.
    /// </summary>
    // CONSUMER: runs for the whole life of the app, waits for messages on the topic and, for each one,
    // validates + saves it (same business code as the file feed) and pushes the live update to the UI.
    // The offset is committed only after the save succeeds, so a failed save is retried, not lost.
    public class KafkaConsumerService(IServiceScopeFactory scopeFactory, IConfiguration config,
        PaymentNotifier notifier, ILogger<KafkaConsumerService> logger) : BackgroundService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// Reads messages from the topic in a loop, with retry and skip rules.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield(); // let the app finish starting; the loop below blocks while waiting for messages

            var topic = config["Kafka:Topic"]!;
            using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
            {
                BootstrapServers = config["Kafka:BootstrapServers"],
                GroupId = config["Kafka:GroupId"],
                AutoOffsetReset = AutoOffsetReset.Earliest, // a new group starts from the oldest message
                EnableAutoCommit = false                    // we commit ourselves, after the save succeeded
            }).Build();

            consumer.Subscribe(topic);
            logger.LogInformation("Kafka consumer listening on topic {Topic}", topic);

            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<Ignore, string>? result = null;
                try
                {
                    result = consumer.Consume(stoppingToken);
                    if (result?.Message == null) continue;

                    await SaveAsync(result.Message.Value);
                    consumer.Commit(result);
                }
                catch (OperationCanceledException)
                {
                    break; // app is shutting down
                }
                catch (Exception ex) when (result != null && ex is JsonException or FormatException)
                {
                    // Poison message: it can never be parsed, so retrying is pointless. Log it and move past it.
                    logger.LogError(ex, "Skipping unreadable Kafka message at {Offset}", result!.TopicPartitionOffset);
                    consumer.Commit(result);
                }
                catch (Exception ex)
                {
                    // Transient failure (database down, broker hiccup): do not commit, go back to the failed
                    // message and try it again after a short pause.
                    logger.LogError(ex, "Kafka consume/save failed, will retry");
                    if (result != null) consumer.Seek(result.TopicPartitionOffset);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }

            consumer.Close();
        }

        /// <summary>
        /// Saves one Kafka message and pushes the live update.
        /// </summary>
        private async Task SaveAsync(string json)
        {
            var payment = JsonSerializer.Deserialize<PaymentEntity>(json, JsonOptions)
                ?? throw new FormatException("Empty Kafka message.");

            using var scope = scopeFactory.CreateScope();
            var business = scope.ServiceProvider.GetRequiredService<IPaymentBusiness>();
            business.InsertBatch(new List<PaymentEntity> { payment }, PaymentConstants.Messaging,
                DateTime.Now.ToString("yyyyMMddHHmmssfff"));
            await notifier.PublishAsync(business);
        }
    }
}
