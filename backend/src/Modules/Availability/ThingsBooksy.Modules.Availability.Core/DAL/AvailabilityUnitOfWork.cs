using ThingsBooksy.Shared.Infrastructure.Postgres;

namespace ThingsBooksy.Modules.Availability.Core.DAL;

internal class AvailabilityUnitOfWork : PostgresUnitOfWork<AvailabilityDbContext>
{
    public AvailabilityUnitOfWork(AvailabilityDbContext dbContext) : base(dbContext) { }
}
