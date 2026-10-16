using BNB.UsecaseEntities;
using BNB.UsecaseRepository.Interfaces;
using BNB.UsecaseServices.Interfaces;

namespace BNB.UsecaseServices
{
    /// <summary>
    /// Business logic for payments: feed parsing, validation and saving.
    /// </summary>
    public class PaymentBusiness(IPaymentRepository repository) : IPaymentBusiness
    {
        /// <summary>
        /// Returns all stored payments.
        /// </summary>
        public List<PaymentDTO> GetAll() => repository.GetAll();

        /// <summary>
        /// Returns stored payments for 'valid' or 'invalid'; anything else throws ArgumentException.
        /// </summary>
        public List<PaymentDTO> GetByStatus(string status)
        {
            if (string.Equals(status, PaymentConstants.Valid, StringComparison.OrdinalIgnoreCase))
                return repository.GetByStatus(PaymentConstants.Valid);

            if (string.Equals(status, PaymentConstants.Invalid, StringComparison.OrdinalIgnoreCase))
                return repository.GetByStatus(PaymentConstants.Invalid);

            throw new ArgumentException($"Unknown status '{status}'. Use 'valid' or 'invalid'.", nameof(status));
        }
        /// <summary>
        /// Returns true when the feed file has not been loaded yet.
        /// </summary>
        public bool FeedExist(string fileName) => repository.FeedExist(Path.GetFileName(fileName));

        /// <summary>
        /// Parses a comma or tab separated flat file; throws FormatException listing every bad line.
        /// </summary>
        public List<PaymentEntity> GetPaymentFromFeed(string fileName)
        {
            var feedName = Path.GetFileName(fileName);
            using var reader = new StreamReader(fileName);
            var payments = new List<PaymentEntity>();
            var errors = new List<string>();
            var lineNo = 0;
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                lineNo++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split('\t', ',').Select(s => s.Trim()).ToArray();
                if (lineNo == 1 && parts.Length > 1 && !int.TryParse(parts[1], out _)) continue; // header

                if (parts.Length != 3
                    || string.IsNullOrEmpty(parts[0])
                    || !int.TryParse(parts[1], out var account)
                    || !int.TryParse(parts[2], out var pnl))
                {
                    errors.Add($"Line {lineNo}: invalid format '{line}'");
                    continue;
                }

                payments.Add(new PaymentEntity
                {
                    SourceSystem = parts[0],
                    AccountNumber = account,
                    PnLAmount = pnl
                });
            }

            if (errors.Count > 0)
                throw new FormatException($"Feed '{feedName}' has invalid records: {string.Join("; ", errors)}");

            return payments;
        }
        /// <summary>
        /// Builds payment records, validates them, saves them (invalid ones too) and returns the counts.
        /// </summary>
        public FeedResult InsertBatch(List<PaymentEntity> payments, string sourceType, string? fileName = null)
        {
            var records = payments.Select(p => new PaymentDTO
            {
                SourceSystem = p.SourceSystem,
                AccountNumber = p.AccountNumber,
                PnLAmount = p.PnLAmount,
                SourceType = sourceType,
                FileName = fileName ?? Guid.NewGuid().ToString()
            }).ToList();

            foreach (var r in records) r.Validate();

            var persisted = repository.InsertBatch(records);
            var invalid = records.Count(r => r.PaymentStatus == PaymentConstants.Invalid);
            return new FeedResult
            {
                TotalRecords = records.Count,
                InvalidRecords = invalid,
                ValidRecords = records.Count - invalid,
                Persisted = persisted
            };
        }
    }
}
