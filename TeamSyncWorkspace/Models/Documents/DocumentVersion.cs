using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models.Documents
{
    public class DocumentVersion
    {
        [Key]
        public int VersionId { get; set; }

        public int DocumentId { get; set; }

        public int VersionNumber { get; set; }

        public string Content { get; set; }

        public int CreatedById { get; set; }

        public DateTime CreatedDate { get; set; }

        [ForeignKey("DocumentId")]
        public CollabDoc Document { get; set; }

        [ForeignKey("CreatedById")]
        public ApplicationUser CreatedBy { get; set; }
    }
}