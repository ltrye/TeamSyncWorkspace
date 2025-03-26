using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models.Documents
{
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        public int? DocumentId { get; set; }

        public int UserId { get; set; }

        public string Content { get; set; }

        public DateTime SentAt { get; set; }

        [ForeignKey("DocumentId")]
        public CollabDoc Document { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
    }
}