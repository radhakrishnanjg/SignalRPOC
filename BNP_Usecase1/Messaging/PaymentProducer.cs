using System.Text.Json;
using BNB.UsecaseEntities;
using Confluent.Kafka;

namespace BNP_Usecase1.Messaging
{
    /// <summary>
    /// Kafka producer that sends payment records to the topic.
    /// </summary>
    // PRODUCER: sends PnL records to the Kafka topic. Real source systems would do this themselves;
    // here it backs the "publish" test endpoint so you can simulate a source system.
    public class PaymentProducer(IConfiguration config) : IDisposable
    {
        private readonly string _topic = config["Kafka:Topic"]!;

        private readonly IProducer<string, string> _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
            Acks = Acks.All,             // wait until the broker has stored the message
            EnableIdempotence = true,    // a retry can never create a duplicate message
            MessageTimeoutMs = 5000      // fail fast (instead of 5 min) when the broker is down
        }).Build();

        /// <summary>
        /// Sends all records to the topic and waits for the broker to confirm them.
        /// </summary>
        public async Task PublishAsync(IEnumerable<PaymentEntity> payments)
        {
            // Send all records at once and wait for the broker to confirm every one of them.
            // The key (account number) keeps records of the same account in order if partitions are added later.
            var sends = payments.Select(p => _producer.ProduceAsync(_topic, new Message<string, string>
            {
                Key = p.AccountNumber.ToString(),
                Value = JsonSerializer.Serialize(new { p.SourceSystem, p.AccountNumber, p.PnLAmount })
            }));
            await Task.WhenAll(sends);
        }

        /// <summary>
        /// Flushes pending messages and closes the producer.
        /// </summary>
        public void Dispose()
        {
            _producer.Flush(TimeSpan.FromSeconds(2));
            _producer.Dispose();
        }
    }
}
