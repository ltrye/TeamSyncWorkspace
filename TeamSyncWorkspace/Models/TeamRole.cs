using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models;

public class TeamRole
{
    [Key]
    public int RoleId { get; set; }

    public string Name { get; set; }
    public string Description { get; set; }

    // Add this property to make roles team-specific
    public int? TeamId { get; set; }

    // Is this a system role (null TeamId) or a custom team role
    public bool IsSystemRole => TeamId == null;

    // Navigation property for team (if applicable)
    [ForeignKey("TeamId")]
    public Team Team { get; set; }

    // Navigation property for permissions
    public ICollection<TeamRolePermission> Permissions { get; set; }
}