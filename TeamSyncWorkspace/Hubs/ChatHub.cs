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

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task NotifyMemberRemoved(string groupName, int userId)
        {
            await Clients.Group(groupName).SendAsync("MemberRemoved", userId);
        }

        public async Task NotifyMemberLeft(string groupName, int userId)
        {
            await Clients.Group(groupName).SendAsync("MemberLeft", userId);
        }

        public async Task NotifyGroupCreated(int chatId, string chatName, List<int> memberIds)
        {
            foreach (var memberId in memberIds)
            {
                await Clients.User(memberId.ToString()).SendAsync("GroupCreated", chatId, chatName);
            }
        }
    }
}
