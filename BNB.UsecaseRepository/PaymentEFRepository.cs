using BNB.UsecaseEntities;
using BNB.UsecaseRepository.EF;
using BNB.UsecaseRepository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BNB.UsecaseRepository
{
    // Entity Framework version of PaymentRepository: same behaviour, no stored procedures or ADO.NET.
    public class PaymentEFRepository(PaymentDbContext db) : IPaymentRepository
    {
        public List<PaymentDTO> GetAll() => Query(null);

        public List<PaymentDTO> GetByStatus(string paymentStatus) => Query(paymentStatus);

        // Same meaning as PaymentRepository: true when the file name is not in the table yet.
        public bool FeedExist(string fileName) =>
            !db.TransactionDetails.AsNoTracking().Any(t => t.FileName == fileName);

        public int Insert(PaymentDTO payment) => InsertBatch(new List<PaymentDTO> { payment });

        public int InsertBatch(List<PaymentDTO> payments)
        {
            if (payments.Count == 0) return 0;

            var now = DateTime.Now;
            db.TransactionDetails.AddRange(payments.Select(p => new TransactionDetail
            {
                SourceType = p.SourceType,
                SourceSystem = p.SourceSystem,
                AccountNumber = p.AccountNumber,
                PnLAmount = p.PnLAmount,
                PaymentStatus = p.PaymentStatus,
                FileName = p.FileName ?? string.Empty,
                CreatedDate = now
            }));

            // One transaction for the whole batch; returns the inserted row count.
            return db.SaveChanges();
        }

        // A null status returns all records.
        private List<PaymentDTO> Query(string? status)
        {
            var rows = db.TransactionDetails.AsNoTracking()
                .Where(t => status == null || t.PaymentStatus == status)
                .OrderBy(t => t.TransactionId)
                .ToList();

            return rows.Select(t =>
            {
                var payment = new PaymentDTO
                {
                    FileName = t.FileName,
                    SourceType = t.SourceType,
                    SourceSystem = t.SourceSystem,
                    AccountNumber = t.AccountNumber ?? 0,
                    PnLAmount = t.PnLAmount ?? 0,
                    CreatedDate = t.CreatedDate ?? default
                };
                payment.Validate(); // status is derived from PnLAmount, matching what was stored
                return payment;
            }).ToList();
        }
    }
}
