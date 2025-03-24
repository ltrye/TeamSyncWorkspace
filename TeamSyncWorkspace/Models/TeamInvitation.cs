using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models
{
    public class TeamInvitation
    {
        [Key]
        public int InvitationId { get; set; }

        public int TeamId { get; set; }

        public int InvitedByUserId { get; set; }

        public int? InvitedUserId { get; set; }

        [Required]
        [EmailAddress]
        public string InvitedEmail { get; set; }

        public DateTime InvitedDate { get; set; }

        public DateTime? AcceptedDate { get; set; }

        public DateTime ExpiryDate { get; set; }

        public bool IsAccepted { get; set; }

        public bool IsDeclined { get; set; }

        [MaxLength(36)]
        public string Token { get; set; }

        public string Role { get; set; } = "Member";

        [ForeignKey("TeamId")]
        public Team Team { get; set; }

        [ForeignKey("InvitedByUserId")]
        public ApplicationUser InvitedBy { get; set; }

        [ForeignKey("InvitedUserId")]
        public ApplicationUser InvitedUser { get; set; }
    }
}