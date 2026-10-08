using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Exceptions;
using ThingsBooksy.Modules.Resources.Core.Features.CreateResourceInstance;
using ThingsBooksy.Modules.Resources.Core.Features.CreateResourceSchema;
using ThingsBooksy.Modules.Resources.Core.Features.DeleteResourceInstance;
using ThingsBooksy.Modules.Resources.Core.Features.DeleteResourceSchema;
using ThingsBooksy.Modules.Resources.Core.Features.GetResourceInstance;
using ThingsBooksy.Modules.Resources.Core.Features.GetResourceInstances;
using ThingsBooksy.Modules.Resources.Core.Features.GetResourceSchema;
using ThingsBooksy.Modules.Resources.Core.Features.GetResourceSchemas;
using ThingsBooksy.Modules.Resources.Core.Features.UpdateResourceInstance;
using ThingsBooksy.Modules.Resources.Core.Features.UpdateResourceSchema;
using ThingsBooksy.Shared.Abstractions.Commands;
using ThingsBooksy.Shared.Abstractions.Exceptions;
using ThingsBooksy.Shared.Abstractions.Queries;
using ThingsBooksy.Shared.Infrastructure.DataProviders;
using ThingsBooksy.Shared.Infrastructure.Messaging.Outbox;
using ThingsBooksy.Shared.Infrastructure.Postgres;

[assembly: InternalsVisibleTo("ThingsBooksy.Modules.Resources.Api")]
[assembly: InternalsVisibleTo("ThingsBooksy.Modules.Resources.Migrations")]
[assembly: InternalsVisibleTo("ThingsBooksy.Modules.Resources.IntegrationTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace ThingsBooksy.Modules.Resources.Core;

internal static class Extensions
{
    public static IServiceCollection AddResourcesCore(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddSingleton<IExceptionToResponseMapper, ResourcesExceptionToResponseMapper>()
            .AddScoped<ICommandHandler<CreateResourceInstanceCommand, Guid>, CreateResourceInstanceCommandHandler>()
            .AddScoped<ICommandHandler<CreateResourceSchemaCommand, Guid>, CreateResourceSchemaCommandHandler>()
            .AddScoped<IQueryHandler<GetResourceSchemaQuery, GetResourceSchemaQueryResult?>, GetResourceSchemaQueryHandler>()
            .AddScoped<IQueryHandler<GetResourceSchemasQuery, IReadOnlyList<GetResourceSchemasQueryResult>>, GetResourceSchemasQueryHandler>()
            .AddScoped<IQueryHandler<GetResourceInstanceQuery, GetResourceInstanceQueryResult?>, GetResourceInstanceQueryHandler>()
            .AddScoped<IQueryHandler<GetResourceInstancesQuery, GetResourceInstancesQueryResult>, GetResourceInstancesQueryHandler>()
            .AddScoped<ICommandHandler<UpdateResourceInstanceCommand>, UpdateResourceInstanceCommandHandler>()
            .AddScoped<ICommandHandler<DeleteResourceInstanceCommand>, DeleteResourceInstanceCommandHandler>()
            .AddScoped<ICommandHandler<UpdateResourceSchemaCommand>, UpdateResourceSchemaCommandHandler>()
            .AddScoped<ICommandHandler<DeleteResourceSchemaCommand>, DeleteResourceSchemaCommandHandler>()
            .AddPostgres<ResourcesDbContext>(configuration, "ThingsBooksy.Modules.Resources.Migrations")
            .AddOutbox<ResourcesDbContext>(configuration)
            .AddUnitOfWork<ResourcesUnitOfWork>()
            .AddDataProviders([typeof(Extensions).Assembly]);
    }
}
