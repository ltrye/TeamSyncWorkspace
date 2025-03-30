using Microsoft.AspNetCore.Identity;

namespace TeamSyncWorkspace.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public Chat Chat { get; set; }
        public bool IsDeleted { get; set; }

        public int UserId { get; set; }
        public ApplicationUser User { get; set; }

        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
