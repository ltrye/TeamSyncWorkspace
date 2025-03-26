using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models.Documents
{
    public class DocumentPermission
    {
        [Key]
        public int PermissionId { get; set; }

        public int DocumentId { get; set; }

        public int UserId { get; set; }

        public bool CanEdit { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }
    }
}