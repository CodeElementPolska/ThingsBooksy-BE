using ThingsBooksy.Shared.Infrastructure.Postgres;

namespace ThingsBooksy.Modules.Calendar.Core.DAL;

internal class CalendarUnitOfWork : PostgresUnitOfWork<CalendarDbContext>
{
    public CalendarUnitOfWork(CalendarDbContext dbContext) : base(dbContext) { }
}
