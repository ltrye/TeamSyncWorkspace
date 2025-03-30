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

            // Lấy danh sách thành viên trong chat (ngoại trừ người gửi)
            var chatMembers = chat.ChatMembers
                .Where(cm => cm.UserId != CurrentUserId)
                .ToList();

            foreach (var member in chatMembers)
            {
                string chatWithName;

                if (!chat.IsGroup) // Kiểm tra nếu là chat 1-1
                {
                    var otherMember = chat.ChatMembers.FirstOrDefault(cm => cm.UserId != CurrentUserId);
                    chatWithName = otherMember != null ? otherMember.User.UserName : "Unknown";
                }
                else // Nếu là group chat
                {
                    chatWithName = chat.Name;
                }

                // Tạo thông báo cho thành viên
                await _notificationService.CreateNotificationAsync(
                    member.UserId,
                    "New Message",
                    $"You have a new message in chat with {chatWithName}: \"{message.Content}\"",
                    $"/Chat/ChatRoom?chatId={chatId}",
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

        public async Task<IActionResult> OnPostLeaveGroupAsync(int chatId, int teamId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var userId = int.Parse(_userManager.GetUserId(User));
            Console.WriteLine($"UserId: {userId}, ChatId: {chatId}");

            // Tìm thành viên trong nhóm
            var chatMember = await _context.ChatMembers
                .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

            if (chatMember == null)
            {
                Console.WriteLine("ChatMember not found.");
                return NotFound("You are not a member of this group.");
            }

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

            return Redirect($"/Teams/Members/Index?teamId={teamId}");
        }
    }
}
