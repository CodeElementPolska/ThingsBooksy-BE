using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Availability.Core.Domain;
using ThingsBooksy.Modules.Availability.Core.ReadModels;
using ThingsBooksy.Shared.Infrastructure.Messaging.Outbox;

namespace ThingsBooksy.Modules.Availability.Core.DAL;

internal class AvailabilityDbContext : DbContext
{
    public DbSet<InboxMessage> Inbox { get; set; } = null!;
    public DbSet<OutboxMessage> Outbox { get; set; } = null!;
    public DbSet<SchemaRuleSet> SchemaRuleSets { get; set; } = null!;
    public DbSet<ResourceRuleSet> ResourceRuleSets { get; set; } = null!;
    public DbSet<AvailabilityRule> AvailabilityRules { get; set; } = null!;
    public DbSet<GroupReadModel> GroupReadModels { get; set; } = null!;
    public DbSet<SchemaReadModel> SchemaReadModels { get; set; } = null!;
    public DbSet<ResourceReadModel> ResourceReadModels { get; set; } = null!;
    public DbSet<PublicHolidayReadModel> PublicHolidayReadModels { get; set; } = null!;

    public AvailabilityDbContext(DbContextOptions<AvailabilityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("availability");
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}
