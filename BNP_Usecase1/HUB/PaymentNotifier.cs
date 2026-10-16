using BNB.UsecaseEntities;
using BNB.UsecaseServices.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BNP_Usecase1.Hubs
{
    /// <summary>
    /// Pushes payment updates from the server to all connected browsers.
    /// </summary>
    public class PaymentNotifier(IHubContext<PaymentHub> hub)
    {
        /// <summary>
        /// Sends the current Valid and Invalid lists to every browser.
        /// </summary>
        public async Task PublishAsync(IPaymentBusiness business)
        {
            await hub.Clients.All.SendAsync("ValidUpdated", business.GetByStatus(PaymentConstants.Valid));
            await hub.Clients.All.SendAsync("InvalidUpdated", business.GetByStatus(PaymentConstants.Invalid));
        }
    }
}
