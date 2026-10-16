using BNB.UsecaseEntities;

namespace BNB.UsecaseServices.Interfaces
{
    public interface IPaymentBusiness
    {
        List<PaymentDTO> GetAll();
        List<PaymentDTO> GetByStatus(string status);
        bool FeedExist(string fileName);
        List<PaymentEntity> GetPaymentFromFeed(string fileName);
        FeedResult InsertBatch(List<PaymentEntity> payments, string sourceType, string? fileName = null);
    }
}
