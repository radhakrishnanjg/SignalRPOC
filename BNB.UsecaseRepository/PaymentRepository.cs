using System.Data;
using System.Text.Json;
using BNB.UsecaseEntities;
using BNB.UsecaseRepository.Interfaces;
using Microsoft.Data.SqlClient;

namespace BNB.UsecaseRepository
{
    public class PaymentRepository(string connectionString) : IPaymentRepository
    {
        private const string ProcName = "Payment.usp_TransactionDetail_Insert";
        private const string GetProcName = "Payment.usp_TransactionDetail_GetByStatus";
        private const string FeedExistProcName = "Payment.usp_TransactionDetail_FeedExist";

        public List<PaymentDTO> GetAll() => Query(null);

        public List<PaymentDTO> GetByStatus(string paymentStatus) => Query(paymentStatus);

        // The SP returns 1 when the file name is not in the table yet, 0 when it is.
        public bool FeedExist(string fileName)
        {
            using var conn = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(FeedExistProcName, conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.Add("@FileName", SqlDbType.VarChar, 400).Value = fileName;
            conn.Open();
            return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
        }

        public int Insert(PaymentDTO payment) => InsertBatch(new List<PaymentDTO> { payment });

        public int InsertBatch(List<PaymentDTO> payments)
        {
            if (payments.Count == 0) return 0;

            var json = JsonSerializer.Serialize(payments.Select(p => new
            {
                p.SourceType,
                p.SourceSystem,
                p.AccountNumber,
                p.PnLAmount,
                p.PaymentStatus,
                p.FileName
            }));

            using var conn = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(ProcName, conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add("@JSON", SqlDbType.VarChar, -1).Value = json;
            conn.Open();
            // The SP returns the inserted row count as a single-value result set.
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // A null status makes the SP return all records.
        private List<PaymentDTO> Query(string? status)
        {
            var result = new List<PaymentDTO>();
            using var conn = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(GetProcName, conn) { CommandType = CommandType.StoredProcedure };
            if (status != null) cmd.Parameters.Add("@PaymentStatus", SqlDbType.VarChar, 10).Value = status;
            conn.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var payment = new PaymentDTO
                {
                    FileName = reader.IsDBNull(0) ? null : reader.GetString(0),
                    SourceType = reader.IsDBNull(1) ? null : reader.GetString(1),
                    SourceSystem = reader.IsDBNull(2) ? null : reader.GetString(2),
                    AccountNumber = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    PnLAmount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                };
                payment.Validate(); // status is derived from PnLAmount, matching what was stored
                result.Add(payment);
            }
            return result;
        }
    }
}
