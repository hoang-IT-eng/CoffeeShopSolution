using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CoffeeShop.Hubs
{
    /// <summary>
    /// Real-time kitchen board. Clients join group <c>kitchen</c> on connect.
    /// </summary>
    [Authorize]
    public class KitchenHub : Hub
    {
        public const string GroupName = "kitchen";

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
            await base.OnConnectedAsync();
        }

        public Task JoinKitchen() => Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
    }
}
