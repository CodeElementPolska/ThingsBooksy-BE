using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Shared.Abstractions.DataProviders;

namespace ThingsBooksy.Modules.Resources.Core.Features.UpdateResourceInstance;

internal interface IUpdateResourceInstanceCommandDataProvider : IDataProvider
{
    Task<ResourceInstance?> GetInstanceWithValuesAsync(Guid instanceId, CancellationToken ct);
    Task<bool> IsOwnerAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<List<ResourcePropertyDefinition>> GetPropertyDefinitionsAsync(Guid resourceSchemaId, CancellationToken ct);
    void RemovePropertyValues(IEnumerable<ResourcePropertyValue> values);
    Task AddPropertyValueAsync(ResourcePropertyValue propertyValue, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
