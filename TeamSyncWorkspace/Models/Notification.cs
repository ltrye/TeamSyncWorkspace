using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        public int UserId { get; set; }

        [Required]
        public string Title { get; set; }

        public string Message { get; set; }

        public string? Link { get; set; }

        public DateTime CreatedDate { get; set; }

        public bool IsRead { get; set; }

        public string Type { get; set; }

        public int? RelatedEntityId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }
    }
}