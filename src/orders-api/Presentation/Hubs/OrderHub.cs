using Microsoft.AspNetCore.SignalR;

namespace OrdersApi.Presentation.Hubs;

public class OrderHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}
