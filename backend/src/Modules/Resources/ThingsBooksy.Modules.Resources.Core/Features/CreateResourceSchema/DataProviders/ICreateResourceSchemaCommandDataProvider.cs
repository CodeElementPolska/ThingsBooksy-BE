using System;
using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.ReadModels;
using ThingsBooksy.Shared.Abstractions.DataProviders;

namespace ThingsBooksy.Modules.Resources.Core.Features.CreateResourceSchema;

internal interface ICreateResourceSchemaCommandDataProvider : IDataProvider
{
    Task<GroupReadModel?> GetGroupAsync(Guid groupId, CancellationToken ct);
    Task<bool> ExistsByGroupAndNameAsync(Guid groupId, string normalizedName, Guid? excludeId, CancellationToken ct);
    Task AddResourceSchemaAsync(ResourceSchema resourceSchema, CancellationToken ct);
    Task AddPropertyDefinitionAsync(ResourcePropertyDefinition definition, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
