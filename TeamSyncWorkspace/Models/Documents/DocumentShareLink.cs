using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models.Documents
{
    public class DocumentShareLink
    {
        [Key]
        public int ShareId { get; set; }

        public int DocumentId { get; set; }

        public string Token { get; set; }

        public int CreatedById { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public bool IsActive { get; set; }

        [ForeignKey("DocumentId")]
        public CollabDoc Document { get; set; }

        [ForeignKey("CreatedById")]
        public ApplicationUser CreatedBy { get; set; }
    }
}