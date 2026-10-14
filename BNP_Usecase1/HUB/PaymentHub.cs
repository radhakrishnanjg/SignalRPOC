using BNB.UsecaseEntities;
using BNB.UsecaseServices.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BNP_Usecase1.Hubs
{
    public class PaymentHub(IPaymentBusiness business, PaymentNotifier notifier) : Hub
    {
        public async Task<FeedResult> AddPayment(List<PaymentDTO> payments)
        {
            if (payments == null || payments.Count == 0)
                throw new HubException("At least one record is required.");

            foreach (var p in payments) p.SourceType = PaymentConstants.External;
            var result = business.InsertBatch(payments);
            await notifier.PublishAsync(business);
            return result;
        }
    }
}
