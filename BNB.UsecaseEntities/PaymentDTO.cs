namespace BNB.UsecaseEntities
{
    public class PaymentDTO
    {
        public string? SourceSystem { get; set; }
        public int AccountNumber { get; set; }
        public int PnLAmount { get; set; }
        public string? SourceType { get; set; }
        public string? PaymentStatus { get; private set; }
        public string? FileName { get; set; }

        // Business rule: PnL amount of zero => Invalid, otherwise Valid.
        // This is the only way the status can change.
        public void Validate() =>
            PaymentStatus = PnLAmount == 0 ? PaymentConstants.Invalid : PaymentConstants.Valid;
    }
}
