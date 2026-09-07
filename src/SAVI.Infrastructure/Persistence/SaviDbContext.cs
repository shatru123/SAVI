using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SAVI.Core.Entities;

namespace SAVI.Infrastructure.Persistence;

public class SaviDbContext : DbContext
{
    public SaviDbContext(DbContextOptions<SaviDbContext> options) : base(options)
    {
    }

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MemoryItem> MemoryItems => Set<MemoryItem>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<TaskStep> TaskSteps => Set<TaskStep>();
    public DbSet<ApiProviderDefinition> ApiProviders => Set<ApiProviderDefinition>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();
    public DbSet<UserSettings> Settings => Set<UserSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Convert all DateTimeOffset properties to binary/ticks for SQLite ordering compatibility
        var dateTimeOffsetConverter = new DateTimeOffsetToBinaryConverter();

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(dateTimeOffsetConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(dateTimeOffsetConverter);
                }
            }
        }

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Title).HasMaxLength(256).IsRequired();
            entity.HasMany(c => c.Messages)
                  .WithOne(m => m.Conversation)
                  .HasForeignKey(m => m.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(c => c.UpdatedAt);
            entity.HasIndex(c => c.IsArchived);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Role).HasConversion<string>();
            entity.Property(m => m.MessageType).HasConversion<string>();
            entity.HasIndex(m => m.ConversationId);
            entity.HasIndex(m => m.Timestamp);
        });

        modelBuilder.Entity<MemoryItem>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Type).HasConversion<string>();
            entity.Property(m => m.Content).IsRequired();
            entity.HasIndex(m => m.Type);
            entity.HasIndex(m => m.CreatedAt);
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.State).HasConversion<string>();
            entity.HasIndex(t => t.State);
            entity.HasIndex(t => t.CreatedAt);
        });

        modelBuilder.Entity<TaskStep>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.State).HasConversion<string>();
            entity.HasIndex(s => s.TaskItemId);
        });

        modelBuilder.Entity<ApiProviderDefinition>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasMaxLength(128).IsRequired();
            entity.Property(p => p.Capability).HasMaxLength(64).IsRequired();
            entity.HasIndex(p => p.Capability);
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.PermissionLevel).HasConversion<string>();
            entity.HasIndex(a => a.Timestamp);
        });

        modelBuilder.Entity<UserSettings>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.ActivePersonality).HasConversion<string>();
        });
    }
}
