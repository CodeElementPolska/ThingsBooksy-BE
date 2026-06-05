using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Availability.Core.DAL;
using ThingsBooksy.Modules.Availability.Core.Events.Handlers;
using ThingsBooksy.Modules.Availability.Core.Exceptions;
using ThingsBooksy.Modules.Availability.Core.Features.GetResourceRules;
using ThingsBooksy.Modules.Availability.Core.Features.GetSchemaRules;
using ThingsBooksy.Modules.Availability.Core.Features.UpsertResourceRules;
using ThingsBooksy.Modules.Availability.Core.Features.UpsertSchemaRules;
using ThingsBooksy.Shared.Abstractions.Commands;
using ThingsBooksy.Shared.Abstractions.Events;
using ThingsBooksy.Shared.Abstractions.Events.Calendar;
using ThingsBooksy.Shared.Abstractions.Events.ManagementGroups;
using ThingsBooksy.Shared.Abstractions.Events.Resources;
using ThingsBooksy.Shared.Abstractions.Exceptions;
using ThingsBooksy.Shared.Abstractions.Queries;
using ThingsBooksy.Shared.Infrastructure.DataProviders;
using ThingsBooksy.Shared.Infrastructure.Messaging.Outbox;
using ThingsBooksy.Shared.Infrastructure.Postgres;

[assembly: InternalsVisibleTo("ThingsBooksy.Modules.Availability.Api")]
[assembly: InternalsVisibleTo("ThingsBooksy.Modules.Availability.Migrations")]
[assembly: InternalsVisibleTo("ThingsBooksy.Modules.Availability.IntegrationTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace ThingsBooksy.Modules.Availability.Core;

internal static class Extensions
{
    public static IServiceCollection AddAvailabilityCore(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddSingleton<IExceptionToResponseMapper, AvailabilityExceptionToResponseMapper>()
            .AddScoped<ICommandHandler<UpsertSchemaRulesCommand>, UpsertSchemaRulesCommandHandler>()
            .AddScoped<ICommandHandler<UpsertResourceRulesCommand>, UpsertResourceRulesCommandHandler>()
            .AddScoped<IQueryHandler<GetSchemaRulesQuery, GetSchemaRulesQueryResult>, GetSchemaRulesQueryHandler>()
            .AddScoped<IQueryHandler<GetResourceRulesQuery, GetResourceRulesQueryResult>, GetResourceRulesQueryHandler>()
            .AddScoped<IEventHandler<GroupCreated>, GroupCreatedHandler>()
            .AddScoped<IEventHandler<ResourceSchemaCreatedEvent>, ResourceSchemaCreatedHandler>()
            .AddScoped<IEventHandler<ResourceInstanceCreatedEvent>, ResourceInstanceCreatedHandler>()
            .AddScoped<IEventHandler<HolidaysRefreshedEvent>, HolidaysRefreshedHandler>()
            .AddDataProviders([typeof(Extensions).Assembly])
            .AddPostgres<AvailabilityDbContext>(configuration, "ThingsBooksy.Modules.Availability.Migrations")
            .AddOutbox<AvailabilityDbContext>(configuration)
            .AddUnitOfWork<AvailabilityUnitOfWork>();
    }
}
