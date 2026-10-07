using System.Text.Json;
using BNB.UsecaseEntities;
using Confluent.Kafka;

namespace BNP_Usecase1.Messaging
{
    // PRODUCER: sends PnL records to the Kafka topic. Real source systems would do this themselves;
    // here it backs the "publish" test endpoint so you can simulate a source system.
    public class PaymentProducer(IConfiguration config) : IDisposable
    {
        private readonly string _topic = config["Kafka:Topic"]!;

        private readonly IProducer<Null, string> _producer = new ProducerBuilder<Null, string>(new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
            MessageTimeoutMs = 5000 // fail fast (instead of 5 min) when the broker is down
        }).Build();

        public async Task PublishAsync(IEnumerable<PaymentDTO> payments)
        {
            foreach (var p in payments)
            {
                var json = JsonSerializer.Serialize(new { p.SourceSystem, p.AccountNumber, p.PnLAmount });
                await _producer.ProduceAsync(_topic, new Message<Null, string> { Value = json });
            }
        }

        public void Dispose()
        {
            _producer.Flush(TimeSpan.FromSeconds(2));
            _producer.Dispose();
        }
    }
}
