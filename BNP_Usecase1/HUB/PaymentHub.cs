using Microsoft.AspNetCore.SignalR;

namespace BNP_Usecase1.Hubs
{
    // Clients only listen on this hub; the server pushes to them (see PaymentNotifier).
    public class PaymentHub : Hub
    {
    }
}
