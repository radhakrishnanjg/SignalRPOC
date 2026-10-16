namespace BNB.UsecaseRepository.EF
{
    /// <summary>
    /// Entity Framework entity for a row of Payment.TransactionDetails.
    /// </summary>
    public class TransactionDetail
    {
        public int TransactionId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? SourceType { get; set; }
        public string? SourceSystem { get; set; }
        public int? AccountNumber { get; set; }
        public int? PnLAmount { get; set; }
        public string? PaymentStatus { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
