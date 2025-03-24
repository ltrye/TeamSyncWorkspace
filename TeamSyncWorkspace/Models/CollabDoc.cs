using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models
{
    public class CollabDoc
    {
        [Key]
        public int DocId { get; set; }

        public string Title { get; set; }
        public string Content { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        // Update this to use Guid
        public string WorkspaceId { get; set; }

        [ForeignKey("WorkspaceId")]
        public Workspace Workspace { get; set; }

        [ForeignKey("CreatedByUserId")]
        public ApplicationUser CreatedByUser { get; set; }


    }
}