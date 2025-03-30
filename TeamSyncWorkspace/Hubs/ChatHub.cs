using Microsoft.AspNetCore.SignalR;
using MySqlX.XDevAPI;
using System.Text.RegularExpressions;

namespace TeamSyncWorkspace.Hubs
{
    public class ChatHub : Hub
    {
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task SendMessageToGroup(string groupName, string userName, string message, int userId, string timestamp)
        {
            await Clients.Group(groupName).SendAsync("ReceiveMessage", userName, message, userId, timestamp);
        }
    }
}
