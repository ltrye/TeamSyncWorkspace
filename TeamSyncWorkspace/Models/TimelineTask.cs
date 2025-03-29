using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models;
public class TimelineTask
{
    [Key]
    public int TaskId { get; set; }
    public string WorkspaceId { get; set; }
    public string TaskDescription { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsCompleted { get; set; }

    [ForeignKey("WorkspaceId")]
    public Workspace Workspace { get; set; }
    
    public int? AssignedId { get; set; }
}