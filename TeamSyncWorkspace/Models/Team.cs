using System;
using System.ComponentModel.DataAnnotations;

namespace TeamSyncWorkspace.Models;

public class Team
{
    [Key]
    public int TeamId { get; set; }
    public string TeamName { get; set; }
    public DateTime CreatedDate { get; set; }

    public ICollection<TeamMember> TeamMembers { get; set; }

    // Single workspace associated with this team
    public Workspace Workspace { get; set; }
}