using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models
{
    public class Folder
    {
        [Key]
        public int FolderId { get; set; }

        public string FolderName { get; set; }
        public int? ParentFolderId { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedDate { get; set; }

        // Update this to use Guid
        public string WorkspaceId { get; set; }

        [ForeignKey("WorkspaceId")]
        public Workspace Workspace { get; set; }

        [ForeignKey("ParentFolderId")]
        public Folder ParentFolder { get; set; }

        [ForeignKey("CreatedByUserId")]
        public ApplicationUser CreatedByUser { get; set; }

        public ICollection<Folder> ChildFolders { get; set; }

    }
}