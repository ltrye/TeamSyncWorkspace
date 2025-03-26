using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Models.Documents;

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
    public DbSet<DocumentComment> DocumentComments { get; set; }
    public DbSet<DocumentVersion> DocumentVersions { get; set; }
    public DbSet<DocumentOperation> DocumentOperations { get; set; }
    public DbSet<DocumentAccessLog> DocumentAccessLogs { get; set; }
    public DbSet<DocumentShareLink> DocumentShareLinks { get; set; }
    public DbSet<DocumentPermission> DocumentPermissions { get; set; }
    // Add to AppDbContext.cs
    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure DocumentComment relationships
        modelBuilder.Entity<DocumentComment>()
            .HasOne(dc => dc.Document)
            .WithMany() // Assuming CollabDoc has a collection of DocumentComments
            .HasForeignKey(dc => dc.DocumentId)
            .OnDelete(DeleteBehavior.Cascade); // Keep cascade delete for DocumentId

        modelBuilder.Entity<DocumentComment>()
            .HasOne(dc => dc.User)
            .WithMany() // No inverse navigation property in ApplicationUser
            .HasForeignKey(dc => dc.UserId)
            .OnDelete(DeleteBehavior.Cascade); // Disable cascade delete for UserId

        modelBuilder.Entity<DocumentComment>()
            .HasOne(dc => dc.ResolvedBy)
            .WithMany() // No inverse navigation property in ApplicationUser
            .HasForeignKey(dc => dc.ResolvedById)
            .IsRequired(false) // ResolvedById is optional
            .OnDelete(DeleteBehavior.SetNull); // Disable cascade delete for ResolvedById
    }
}
