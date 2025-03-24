using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSyncWorkspace.Models;

public class TeamMember
{
    [Key]
    public int TeamMemberId { get; set; }
    public int TeamId { get; set; }
    public int UserId { get; set; }
    public DateTime JoinedDate { get; set; }
    public string Role { get; set; } = "Member"; // Default role

    [ForeignKey("TeamId")]
    public Team Team { get; set; }
    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; }
}