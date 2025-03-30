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

    public DbSet<Chat> Chats { get; set; }
    public DbSet<ChatMember> ChatMembers { get; set; }
    public DbSet<Message> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        modelBuilder.Entity<ChatMessage>()
                    .HasOne(cm => cm.Document)
                    .WithMany() // Assuming CollabDoc has a collection of ChatMessages
                    .HasForeignKey(cm => cm.DocumentId)
                    .OnDelete(DeleteBehavior.SetNull); // Keep cascade delete for DocumentId

        modelBuilder.Entity<ChatMessage>()
            .HasOne(cm => cm.User)
            .WithMany() // No inverse navigation property in ApplicationUser
            .HasForeignKey(cm => cm.UserId)
            .OnDelete(DeleteBehavior.NoAction); // Disable cascade delete for UserId

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
            .OnDelete(DeleteBehavior.NoAction); // Disable cascade delete for UserId

        modelBuilder.Entity<DocumentComment>()
            .HasOne(dc => dc.ResolvedBy)
            .WithMany() // No inverse navigation property in ApplicationUser
            .HasForeignKey(dc => dc.ResolvedById)
            .IsRequired(false) // ResolvedById is optional
            .OnDelete(DeleteBehavior.NoAction); // Disable cascade delete for ResolvedById

        modelBuilder.Entity<DocumentOperation>()
            .HasOne(dc => dc.User)
            .WithMany() // Assuming DocumentComment has a collection of child comments
            .HasForeignKey(dc => dc.UserId)
            .IsRequired(false) // ParentCommentId is optional
            .OnDelete(DeleteBehavior.NoAction); // Disable cascade delete for ParentCommentId


        modelBuilder.Entity<DocumentShareLink>()
            .HasOne(dsl => dsl.Document)
            .WithMany() // Assuming CollabDoc has a collection of DocumentShareLinks
            .HasForeignKey(dsl => dsl.DocumentId)
            .OnDelete(DeleteBehavior.NoAction); // Keep cascade delete for DocumentId
        modelBuilder.Entity<DocumentVersion>()
            .HasOne(dv => dv.Document)
            .WithMany() // Assuming CollabDoc has a collection of DocumentVersions
            .HasForeignKey(dv => dv.DocumentId)
            .OnDelete(DeleteBehavior.NoAction); // Keep cascade delete for DocumentId

        // Cấu hình Chat
        modelBuilder.Entity<Chat>()
            .HasMany(c => c.ChatMembers)
            .WithOne(cm => cm.Chat)
            .HasForeignKey(cm => cm.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Chat>()
            .HasMany(c => c.Messages)
            .WithOne(m => m.Chat)
            .HasForeignKey(m => m.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cấu hình ChatMember
        modelBuilder.Entity<ChatMember>()
            .HasOne(cm => cm.Chat)
            .WithMany(c => c.ChatMembers)
            .HasForeignKey(cm => cm.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMember>()
            .HasOne(cm => cm.User)
            .WithMany() // Không cần thuộc tính ngược trong ApplicationUser
            .HasForeignKey(cm => cm.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Cấu hình Message
        modelBuilder.Entity<Message>()
            .HasOne(m => m.Chat)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.User)
            .WithMany() // Không cần thuộc tính ngược trong ApplicationUser
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
