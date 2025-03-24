using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TeamSyncWorkspace.Models;

namespace TeamSyncWorkspace.Data;
public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Team> Teams { get; set; }

    public DbSet<TeamMember> TeamMembers { get; set; }
    public DbSet<Workspace> Workspaces { get; set; }
    public DbSet<CollabDoc> CollabDocs { get; set; }
    public DbSet<Folder> Folders { get; set; }
    public DbSet<Models.File> Files { get; set; }
    public DbSet<TimelineTask> TimelineTasks { get; set; }
    public DbSet<AIMember> AIMembers { get; set; }
    public DbSet<DocAction> DocActions { get; set; }
    public DbSet<TeamRole> TeamRoles { get; set; }
    public DbSet<TeamRolePermission> TeamRolePermissions { get; set; }
    public DbSet<TeamInvitation> TeamInvitations { get; set; }
    public DbSet<Notification> Notifications { get; set; }
}