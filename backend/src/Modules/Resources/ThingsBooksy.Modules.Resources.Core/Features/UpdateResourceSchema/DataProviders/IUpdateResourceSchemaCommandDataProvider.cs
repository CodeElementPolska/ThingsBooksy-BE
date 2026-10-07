using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.ReadModels;
using ThingsBooksy.Shared.Abstractions.DataProviders;

namespace ThingsBooksy.Modules.Resources.Core.Features.UpdateResourceSchema;

internal interface IUpdateResourceSchemaCommandDataProvider : IDataProvider
{
    Task<ResourceSchema?> GetResourceSchemaWithDefinitionsAsync(Guid typeId, CancellationToken ct);
    Task<GroupReadModel?> GetGroupAsync(Guid groupId, CancellationToken ct);
    Task<bool> ExistsByGroupAndNameAsync(Guid groupId, string normalizedName, Guid? excludeId, CancellationToken ct);
    void RemovePropertyDefinitions(IEnumerable<ResourcePropertyDefinition> definitions);
    Task AddPropertyDefinitionAsync(ResourcePropertyDefinition definition, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
