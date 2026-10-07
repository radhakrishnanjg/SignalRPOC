using System.Text.Json;
using BNB.UsecaseEntities;
using BNB.UsecaseServices.Interfaces;
using BNP_Usecase1.Hubs;
using Confluent.Kafka;

namespace BNP_Usecase1.Messaging
{
    // CONSUMER: runs for the whole life of the app, waits for messages on the topic and, for each one,
    // validates + saves it (same business code as the file feed) and pushes the live update to the UI.
    public class KafkaConsumerService(IServiceScopeFactory scopeFactory, IConfiguration config,
        PaymentNotifier notifier, ILogger<KafkaConsumerService> logger) : BackgroundService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield(); // let the app finish starting; the loop below blocks while waiting for messages

            var topic = config["Kafka:Topic"]!;
            using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
            {
                BootstrapServers = config["Kafka:BootstrapServers"],
                GroupId = config["Kafka:GroupId"],
                AutoOffsetReset = AutoOffsetReset.Earliest // a new group starts from the oldest message
            }).Build();

            consumer.Subscribe(topic);
            logger.LogInformation("Kafka consumer listening on topic {Topic}", topic);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    if (result?.Message != null) Save(result.Message.Value);
                }
                catch (OperationCanceledException)
                {
                    break; // app is shutting down
                }
                catch (Exception ex)
                {
                    // One bad message or a broker hiccup must not stop the consumer.
                    logger.LogError(ex, "Kafka consume/save failed");
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }

            consumer.Close();
        }

        private void Save(string json)
        {
            var payment = JsonSerializer.Deserialize<PaymentDTO>(json, JsonOptions)
                ?? throw new FormatException("Empty Kafka message.");
            payment.SourceType = PaymentConstants.Realtime;
            payment.FileName = DateTime.Now.ToString("yyyyMMddhhmmssfff");

            using var scope = scopeFactory.CreateScope();
            var business = scope.ServiceProvider.GetRequiredService<IPaymentBusiness>();
            business.InsertBatch(new List<PaymentDTO> { payment });
            notifier.PublishAsync(business).GetAwaiter().GetResult();
        }
    }
}
