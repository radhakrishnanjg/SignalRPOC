using BNB.UsecaseEntities;

namespace BNB.UsecaseRepository.Interfaces
{
    public interface IPaymentRepository
    {
        List<PaymentDTO> GetAll();
        List<PaymentDTO> GetByStatus(string paymentStatus);
        int Insert(PaymentDTO payment);
        int InsertBatch(List<PaymentDTO> payments);
        bool FeedExist(string fileName);
    }
}
