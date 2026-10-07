using BNB.UsecaseEntities;
using BNB.UsecaseRepository.Interfaces;
using BNB.UsecaseServices.Interfaces;

namespace BNB.UsecaseServices
{
    public class PaymentBusiness(IPaymentRepository repository) : IPaymentBusiness
    {
        public List<PaymentDTO> GetAll() => repository.GetAll();

        // status is "Valid" or "Invalid" (case-insensitive); anything else throws ArgumentException.
        public List<PaymentDTO> GetByStatus(string status)
        {
            if (string.Equals(status, PaymentConstants.Valid, StringComparison.OrdinalIgnoreCase))
                return repository.GetByStatus(PaymentConstants.Valid);

            if (string.Equals(status, PaymentConstants.Invalid, StringComparison.OrdinalIgnoreCase))
                return repository.GetByStatus(PaymentConstants.Invalid);

            throw new ArgumentException($"Unknown status '{status}'. Use 'valid' or 'invalid'.", nameof(status));
        }

        public int Insert(PaymentDTO payment)
        {
            payment.FileName ??= PaymentConstants.Realtime; // FileName column is NOT NULL
            payment.Validate();
            return repository.Insert(payment);
        }

        // True when the feed file has NOT been loaded yet (file name not found in the table).
        public bool FeedExist(string fileName) => repository.FeedExist(Path.GetFileName(fileName));

        // Expected columns (comma or tab separated): SourceSystem,AccountNumber,PnLAmount (header row optional).
        // Throws FormatException listing every malformed line, so a bad file is never partly loaded.
        // fileName is the full path of the flat file; only the name part is stored with the records.
        public List<PaymentDTO> GetPaymentFromFeed(string fileName)
        {
            var feedName = Path.GetFileName(fileName);
            using var reader = new StreamReader(fileName);
            var payments = new List<PaymentDTO>();
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

                payments.Add(new PaymentDTO
                {
                    SourceSystem = parts[0],
                    AccountNumber = account,
                    PnLAmount = pnl,
                    SourceType = PaymentConstants.FileFeed,
                    FileName = feedName
                });
            }

            if (errors.Count > 0)
                throw new FormatException($"Feed '{feedName}' has invalid records: {string.Join("; ", errors)}");

            return payments;
        }

        // Validates every record and persists them (invalid ones included).
        public FeedResult InsertBatch(List<PaymentDTO> payments)
        {
            foreach (var p in payments)
            {
                p.FileName ??= PaymentConstants.Realtime; // FileName column is NOT NULL
                p.Validate();
            }

            var persisted = repository.InsertBatch(payments);
            var invalid = payments.Count(p => p.PaymentStatus == PaymentConstants.Invalid);
            return new FeedResult
            {
                TotalRecords = payments.Count,
                InvalidRecords = invalid,
                ValidRecords = payments.Count - invalid,
                Persisted = persisted
            };
        }
    }
}
