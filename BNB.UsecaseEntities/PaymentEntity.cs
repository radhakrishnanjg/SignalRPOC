namespace BNB.UsecaseEntities
{ 
    /// <summary>
    /// Source data of a payment as sent by a flat file, the UI or Kafka.
    /// </summary>
    public class PaymentEntity
    {
        // Unique id set by the producer; lets the database ignore a message that is delivered twice. Null for file/UI records.
        public Guid? MessageId { get; set; }
        public string? SourceSystem { get; set; }
        public int AccountNumber { get; set; }
        public int PnLAmount { get; set; }
    }
}
