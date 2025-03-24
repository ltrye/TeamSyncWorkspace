using System.ComponentModel.DataAnnotations;

namespace TeamSyncWorkspace.Models;

using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser<int> // Use int as PK type to match your schema
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public int RoleId { get; set; } // Add your custom field
    public ICollection<TeamMember> TeamMemberships { get; set; }
    public ICollection<DocAction> DocActions { get; set; }
}