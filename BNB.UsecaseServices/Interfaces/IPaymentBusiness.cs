using BNB.UsecaseEntities;

namespace BNB.UsecaseServices.Interfaces
{
    public interface IPaymentBusiness
    {
        List<PaymentDTO> GetAll();
        List<PaymentDTO> GetByStatus(string status);
        int Insert(PaymentDTO payment);
        bool FeedExist(string fileName);
        List<PaymentDTO> GetPaymentFromFeed(string fileName);
        FeedResult InsertBatch(List<PaymentDTO> payments);
    }
}
