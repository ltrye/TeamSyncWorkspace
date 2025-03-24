using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models
{
    public class AIMember
    {
        [Key]
        public int AIMemberId { get; set; }

        public string Name { get; set; }
        public string Role { get; set; }
        public string AvatarUrl { get; set; }
        public string ApiKey { get; set; }
        public string ModelName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }

        // Update this to use Guid
        public string WorkspaceId { get; set; }

        [ForeignKey("WorkspaceId")]
        public Workspace Workspace { get; set; }
    }
}
