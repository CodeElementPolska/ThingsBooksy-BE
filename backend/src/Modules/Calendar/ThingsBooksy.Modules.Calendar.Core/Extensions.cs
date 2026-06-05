using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Calendar.Core.DAL;
using ThingsBooksy.Modules.Calendar.Core.Services;
using ThingsBooksy.Shared.Infrastructure.DataProviders;
using ThingsBooksy.Shared.Infrastructure.Messaging.Outbox;
using ThingsBooksy.Shared.Infrastructure.Postgres;

[assembly: InternalsVisibleTo("ThingsBooksy.Modules.Calendar.Api")]
[assembly: InternalsVisibleTo("ThingsBooksy.Modules.Calendar.Migrations")]
[assembly: InternalsVisibleTo("ThingsBooksy.Modules.Calendar.IntegrationTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace ThingsBooksy.Modules.Calendar.Core;

internal static class Extensions
{
    public static IServiceCollection AddCalendarCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("nager-date", c => c.BaseAddress = new Uri("https://date.nager.at/"));
        services.AddHostedService<HolidayRefreshService>();

        return services
            .AddDataProviders([typeof(Extensions).Assembly])
            .AddPostgres<CalendarDbContext>(configuration, "ThingsBooksy.Modules.Calendar.Migrations")
            .AddOutbox<CalendarDbContext>(configuration)
            .AddUnitOfWork<CalendarUnitOfWork>();
    }
}
