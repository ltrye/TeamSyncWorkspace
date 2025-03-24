using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models;

public class TeamRolePermission
{
    [Key]
    public int PermissionId { get; set; }
    public int RoleId { get; set; }
    public string Action { get; set; } // The action name (e.g., "InviteMembers", "CreateWorkspace")

    [ForeignKey("RoleId")]
    public TeamRole Role { get; set; }
}