namespace BNB.UsecaseEntities
{ 
    /// <summary>
    /// Source data of a payment as sent by a flat file, the UI or Kafka.
    /// </summary>
    public class PaymentEntity
    {
        public string? SourceSystem { get; set; }
        public int AccountNumber { get; set; }
        public int PnLAmount { get; set; }
    }
}
