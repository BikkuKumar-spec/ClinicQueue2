using Microsoft.AspNetCore.SignalR;

namespace ClinicQueue.Hubs
{
    public class DashboardHub : Hub
    {
        public async Task NotifySlotBooked()
        {
            await Clients.All.SendAsync("SlotBooked");
        }

        public async Task NotifyStatusChanged()
        {
            await Clients.All.SendAsync("StatusChanged");
        }

        public async Task NotifyQueueUpdated()
        {
            await Clients.All.SendAsync("QueueUpdated");
        }
    }
}
