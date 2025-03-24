using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models;

public class DocAction
{
    [Key]
    public int ActionId { get; set; }
    public int DocId { get; set; }
    public int? UserId { get; set; } // Nullable for AI actions
    public int? AIMemberId { get; set; } // Nullable for user actions
    public string ActionType { get; set; } // e.g., "Text", "Draw"
    public string ActionData { get; set; } // JSON
    public DateTime Timestamp { get; set; }

    [ForeignKey("DocId")]
    public CollabDoc CollabDoc { get; set; }
    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; }
    [ForeignKey("AIMemberId")]
    public AIMember AIMember { get; set; }
}
