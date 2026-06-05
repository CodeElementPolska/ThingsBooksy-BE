using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Calendar.Core.Domain;
using ThingsBooksy.Shared.Infrastructure.Messaging.Outbox;

namespace ThingsBooksy.Modules.Calendar.Core.DAL;

internal class CalendarDbContext : DbContext
{
    public DbSet<InboxMessage> Inbox { get; set; } = null!;
    public DbSet<OutboxMessage> Outbox { get; set; } = null!;
    public DbSet<PublicHolidayFetchLog> FetchLogs { get; set; } = null!;

    public CalendarDbContext(DbContextOptions<CalendarDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("calendar");
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}
