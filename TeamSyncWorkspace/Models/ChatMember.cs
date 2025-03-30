using Microsoft.AspNetCore.Identity;

namespace TeamSyncWorkspace.Models
{
    public class ChatMember
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public Chat Chat { get; set; }
        public int UserId { get; set; }
        public ApplicationUser User { get; set; }
        public bool IsAdmin { get; set; }
    }
}
