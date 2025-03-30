using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Hubs;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

namespace TeamSyncWorkspace.Pages.Chat
{
    public class ChatRoomModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotificationService _notificationService;

        public ChatRoomModel(AppDbContext context, UserManager<ApplicationUser> userManager, NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        public Models.Chat Chat { get; set; }
        public int ChatId { get; set; }
        public int TeamId { get; set; }
        public List<Message> Messages { get; set; }
        [BindProperty]
        public string Content { get; set; }
        public int CurrentUserId { get; set; }
        public string CurrentUserName { get; set; }

        public async Task<IActionResult> OnGetAsync(int chatId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            CurrentUserId = int.Parse(_userManager.GetUserId(User));
            ChatId = chatId;

            Chat = await _context.Chats
                .Include(c => c.Messages)
                .ThenInclude(m => m.User)
                .Include(c => c.ChatMembers)
                .ThenInclude(cm => cm.User)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (Chat == null)
            {
                return NotFound($"Chat with ID {chatId} not found.");
            }

            TeamId = Chat.TeamId;
            Messages = Chat.Messages.OrderBy(m => m.Timestamp).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostSendMessageAsync(int chatId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            CurrentUserId = int.Parse(_userManager.GetUserId(User));
            CurrentUserName = user.UserName;

            if (string.IsNullOrWhiteSpace(Content))
            {
                ModelState.AddModelError(string.Empty, "Message content cannot be empty.");
                return await OnGetAsync(chatId);
            }

            var chat = await _context.Chats
                .Include(c => c.ChatMembers)
                .ThenInclude(cm => cm.User)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null)
            {
                return NotFound($"Chat with ID {chatId} not found.");
            }

            // Tạo tin nhắn mới
            var message = new Message
            {
                ChatId = chatId,
                UserId = CurrentUserId,
                IsDeleted = false,
                Content = Content,
                Timestamp = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Gửi thông báo đến các thành viên khác
            var chatMembers = chat.ChatMembers
                .Where(cm => cm.UserId != CurrentUserId)
                .ToList();

            foreach (var member in chatMembers)
            {
                string chatWithName = chat.IsGroup ? chat.Name : member.User.UserName;

                await _notificationService.CreateNotificationAsync(
                    member.UserId,
                    "New Message",
                    $"You have a new message in chat with {chatWithName}: \"{message.Content}\"",
                    $"/Chat/ChatRoom/{chatId}",
                    "Message",
                    chatId
                );
            }

            Content = string.Empty;

            var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
            await hubContext.Clients.Group(chatId.ToString())
                .SendAsync("ReceiveMessage", CurrentUserName, message.Content, CurrentUserId, message.Timestamp.ToString("o"));

            return RedirectToPage(new { chatId });
        }

        public async Task<IActionResult> OnPostDeleteMessageAsync(int messageId)
        {
            var currentUserId = int.Parse(_userManager.GetUserId(User));

            // Lấy tin nhắn từ cơ sở dữ liệu
            var message = await _context.Messages.FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null)
            {
                return NotFound($"Message with ID {messageId} not found.");
            }

            // Kiểm tra quyền xóa tin nhắn
            if (message.UserId != currentUserId)
            {
                return Forbid();
            }

            // Đánh dấu tin nhắn là đã bị xóa
            message.IsDeleted = true;
            await _context.SaveChangesAsync();

            return RedirectToPage(new { chatId = message.ChatId });
        }

        public async Task<IActionResult> OnPostRemoveMemberAsync(int chatId, int userId)
        {
            var currentUserId = int.Parse(_userManager.GetUserId(User));

            // Lấy thông tin group chat
            var chat = await _context.Chats
                .Include(c => c.ChatMembers)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null || !chat.IsGroup)
            {
                return NotFound("Chat not found or is not a group chat.");
            }

            // Kiểm tra quyền xóa thành viên
            if (chat.CreatedBy != currentUserId && !chat.ChatMembers.Any(cm => cm.UserId == currentUserId && cm.IsAdmin))
            {
                return Forbid(); // Không có quyền xóa thành viên
            }

            // Xóa thành viên khỏi group chat
            var memberToRemove = chat.ChatMembers.FirstOrDefault(cm => cm.UserId == userId);
            if (memberToRemove != null)
            {
                _context.ChatMembers.Remove(memberToRemove);
                await _context.SaveChangesAsync();
            }

            // Thông báo qua SignalR cho thành viên bị xóa
            var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
            await hubContext.Clients.User(userId.ToString())
                .SendAsync("RemovedFromGroup", chatId.ToString(), chat.TeamId);

            // Thông báo cho các thành viên còn lại trong group
            await hubContext.Clients.Group(chatId.ToString())
                .SendAsync("MemberRemoved", userId);

            await _notificationService.CreateNotificationAsync(
                    userId,
                    "Removed from Group",
                    $"You have been removed from the group chat '{chat.Name}'.",
                    $"/Teams/Members/Index?teamId={chat.TeamId}",
                    "Removal"
                );

            return RedirectToPage(new { chatId });
        }

        public async Task<IActionResult> OnPostLeaveGroupAsync(int chatId, int teamId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var userId = int.Parse(_userManager.GetUserId(User));

            // Tìm thành viên trong nhóm
            var chatMember = await _context.ChatMembers
                .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

            if (chatMember == null)
            {
                return NotFound("You are not a member of this group.");
            }

            // Kiểm tra nếu người dùng là admin
            bool isAdmin = chatMember.IsAdmin;

            // Xóa thành viên khỏi nhóm
            _context.ChatMembers.Remove(chatMember);
            await _context.SaveChangesAsync();

            // Nếu nhóm không còn thành viên nào, xóa nhóm
            var remainingMembers = await _context.ChatMembers
                .Where(cm => cm.ChatId == chatId)
                .ToListAsync();

            if (!remainingMembers.Any())
            {
                var chat = await _context.Chats.FindAsync(chatId);
                if (chat != null)
                {
                    _context.Chats.Remove(chat);
                    await _context.SaveChangesAsync();
                }
            }
            else if (isAdmin)
            {
                var nextMember = remainingMembers.FirstOrDefault();
                if (nextMember != null)
                {
                    nextMember.IsAdmin = true;
                    _context.ChatMembers.Update(nextMember);
                    await _context.SaveChangesAsync();
                }
            }

            var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
            await hubContext.Clients.Group(chatId.ToString())
                .SendAsync("MemberLeft", userId);

            return Redirect($"/Teams/Members/Index?teamId={teamId}");
        }

        public async Task<IActionResult> OnPostInviteMemberAsync(int chatId, string userEmail)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Lấy thông tin group chat
            var chat = await _context.Chats
                .Include(c => c.ChatMembers)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null || !chat.IsGroup)
            {
                return NotFound("Chat not found or is not a group chat.");
            }

            // Kiểm tra nếu người dùng hiện tại có quyền mời thành viên
            if (!chat.ChatMembers.Any(cm => cm.UserId == int.Parse(_userManager.GetUserId(User)) && cm.IsAdmin))
            {
                return Forbid(); // Chỉ admin mới có quyền mời thành viên
            }

            // Tìm người dùng theo email
            var userToInvite = await _userManager.FindByEmailAsync(userEmail);
            if (userToInvite == null)
            {
                ModelState.AddModelError(string.Empty, "User with this email does not exist.");
                return await OnGetAsync(chatId);
            }

            // Kiểm tra nếu người dùng đã là thành viên của group
            if (chat.ChatMembers.Any(cm => cm.UserId == userToInvite.Id))
            {
                ModelState.AddModelError(string.Empty, "User is already a member of this group.");
                return await OnGetAsync(chatId);
            }

            // Thêm người dùng vào group chat
            var newMember = new ChatMember
            {
                ChatId = chatId,
                UserId = userToInvite.Id,
                IsAdmin = false // Thành viên mới không phải admin
            };

            _context.ChatMembers.Add(newMember);
            await _context.SaveChangesAsync();

            // Gửi thông báo cho người được mời
            await _notificationService.CreateNotificationAsync(
                userToInvite.Id,
                "Group Invitation",
                $"You have been invited to join the group chat '{chat.Name}'.",
                $"/Chat/ChatRoom/{chatId}",
                "Invitation"
            );

            return RedirectToPage(new { chatId });
        }
    }
}