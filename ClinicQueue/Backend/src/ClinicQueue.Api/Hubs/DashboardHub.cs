using Microsoft.AspNetCore.SignalR;

namespace ClinicQueue.Api.Hubs;

public class DashboardHub : Hub
{
    public Task NotifySlotBooked() => Clients.All.SendAsync("SlotBooked");

    public Task NotifyStatusChanged() => Clients.All.SendAsync("StatusChanged");

    public Task NotifyQueueUpdated() => Clients.All.SendAsync("QueueUpdated");
}
