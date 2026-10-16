using BNB.UsecaseEntities;
using BNB.UsecaseRepository.EF;
using BNB.UsecaseRepository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BNB.UsecaseRepository
{
    /// <summary>
    /// Entity Framework repository with the same behaviour as PaymentRepository.
    /// </summary>
    public class PaymentEFRepository(PaymentDbContext db) : IPaymentRepository
    {
        /// <summary>
        /// Returns all stored payments.
        /// </summary>
        public List<PaymentDTO> GetAll() => Query(null);

        /// <summary>
        /// Returns stored payments with the given status.
        /// </summary>
        public List<PaymentDTO> GetByStatus(string paymentStatus) => Query(paymentStatus);

        /// <summary>
        /// Returns true when the file name is not in the table yet.
        /// </summary>
        public bool FeedExist(string fileName) =>
            !db.TransactionDetails.AsNoTracking().Any(t => t.FileName == fileName);

        /// <summary>
        /// Saves one payment.
        /// </summary>
        public int Insert(PaymentDTO payment) => InsertBatch(new List<PaymentDTO> { payment });

        /// <summary>
        /// Saves a list of payments in one transaction and returns the row count.
        /// </summary>
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

        /// <summary>
        /// Reads payments in id order and maps them to DTOs; a null status returns all.
        /// </summary>
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
