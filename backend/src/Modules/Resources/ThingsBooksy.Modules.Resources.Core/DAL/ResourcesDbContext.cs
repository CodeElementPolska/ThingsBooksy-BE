using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.ReadModels;
using ThingsBooksy.Shared.Infrastructure.Messaging.Outbox;

namespace ThingsBooksy.Modules.Resources.Core.DAL;

internal class ResourcesDbContext : DbContext
{
    public DbSet<ResourceSchema> ResourceSchemas { get; set; } = null!;
    public DbSet<ResourcePropertyDefinition> ResourcePropertyDefinitions { get; set; } = null!;
    public DbSet<ResourceInstance> ResourceInstances { get; set; } = null!;
    public DbSet<ResourcePropertyValue> ResourcePropertyValues { get; set; } = null!;
    public DbSet<GroupReadModel> GroupReadModels { get; set; } = null!;
    public DbSet<GroupMemberReadModel> GroupMemberReadModels { get; set; } = null!;
    public DbSet<InboxMessage> Inbox { get; set; } = null!;
    public DbSet<OutboxMessage> Outbox { get; set; } = null!;

    public ResourcesDbContext(DbContextOptions<ResourcesDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("resources");
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}
