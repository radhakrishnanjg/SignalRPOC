using BNB.UsecaseEntities;
using BNB.UsecaseServices.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BNP_Usecase1.Hubs
{
    // Call PublishAsync after new records are saved. It pushes the same lists that
    // GetByStatus("valid") and GetByStatus("invalid") return to every connected browser.
    public class PaymentNotifier(IHubContext<PaymentHub> hub)
    {
        public async Task PublishAsync(IPaymentBusiness business)
        {
            await hub.Clients.All.SendAsync("ValidUpdated", business.GetByStatus(PaymentConstants.Valid));
            await hub.Clients.All.SendAsync("InvalidUpdated", business.GetByStatus(PaymentConstants.Invalid));
        }
    }
}
