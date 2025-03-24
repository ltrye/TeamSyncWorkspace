using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models;

public class Workspace
{
    [Key]
    public string WorkspaceId { get; set; }

    public int TeamId { get; set; }
    public string WorkspaceName { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedDate { get; set; }

    // One-to-one relationship with Team
    [ForeignKey("TeamId")]
    public Team Team { get; set; }

    public ICollection<CollabDoc> CollabDocs { get; set; }
    public ICollection<Folder> Folders { get; set; }
    public ICollection<TimelineTask> TimelineTasks { get; set; }
    public ICollection<AIMember> AIMembers { get; set; }
}