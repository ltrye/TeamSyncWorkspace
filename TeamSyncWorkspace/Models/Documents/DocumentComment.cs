using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models.Documents
{
    public class DocumentComment
    {
        [Key]
        public int CommentId { get; set; }

        public int DocumentId { get; set; }

        public int UserId { get; set; }

        public string Content { get; set; }

        public string RangeStart { get; set; }

        public string RangeEnd { get; set; }

        public DateTime CreatedDate { get; set; }

        public bool IsResolved { get; set; }

        public int? ResolvedById { get; set; }

        public DateTime? ResolvedDate { get; set; }

        [ForeignKey("DocumentId")]
        public CollabDoc Document { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        [ForeignKey("ResolvedById")]
        public ApplicationUser? ResolvedBy { get; set; }
    }
}