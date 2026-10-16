using BNB.UsecaseEntities;
using BNB.UsecaseServices.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BNP_Usecase1.Hubs
{
    /// <summary>
    /// SignalR hub at /hubs/payment: browsers call it to add payments.
    /// </summary>
    public class PaymentHub(IPaymentBusiness business, PaymentNotifier notifier) : Hub
    {
        /// <summary>
        /// Saves payments sent from the UI, then pushes the updated lists to all browsers.
        /// </summary>
        public async Task<FeedResult> AddPayment(List<PaymentEntity> payments)
        {
            if (payments == null || payments.Count == 0)
                throw new HubException("At least one record is required.");

            var result = business.InsertBatch(payments, PaymentConstants.External);
            await notifier.PublishAsync(business);
            return result;
        }
    }
}
