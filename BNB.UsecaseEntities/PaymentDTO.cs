namespace BNB.UsecaseEntities
{
    /// <summary>
    /// A stored payment: the source data plus source type, status, file name and created date.
    /// </summary>
    public class PaymentDTO : PaymentEntity
    {
        public string? SourceType { get; set; }
        public string? PaymentStatus { get; private set; }
        public string? FileName { get; set; }
        public DateTime CreatedDate { get; set; } 
        /// <summary>
        /// Sets the status: Invalid when the PnL amount is 0, otherwise Valid.
        /// </summary>
        public void Validate() =>
            PaymentStatus = PnLAmount == 0 ? PaymentConstants.Invalid : PaymentConstants.Valid;
    }
}
