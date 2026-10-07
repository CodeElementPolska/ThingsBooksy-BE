using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;

namespace ThingsBooksy.Modules.Resources.Core.Features.UpdateResourceInstance;

internal sealed class UpdateResourceInstanceCommandDataProvider : IUpdateResourceInstanceCommandDataProvider
{
    private readonly ResourcesDbContext _dbContext;

    public UpdateResourceInstanceCommandDataProvider(ResourcesDbContext dbContext)
        => _dbContext = dbContext;

    public Task<ResourceInstance?> GetInstanceWithValuesAsync(Guid instanceId, CancellationToken ct)
        => _dbContext.ResourceInstances
            .Include(x => x.PropertyValues)
            .FirstOrDefaultAsync(x => x.Id == instanceId, ct);

    public Task<bool> IsOwnerAsync(Guid groupId, Guid userId, CancellationToken ct)
        => _dbContext.GroupReadModels.AnyAsync(g => g.Id == groupId && g.OwnerId == userId, ct);

    public Task<List<ResourcePropertyDefinition>> GetPropertyDefinitionsAsync(Guid resourceSchemaId, CancellationToken ct)
        => _dbContext.ResourcePropertyDefinitions.Where(d => d.ResourceSchemaId == resourceSchemaId).ToListAsync(ct);

    public void RemovePropertyValues(IEnumerable<ResourcePropertyValue> values)
        => _dbContext.ResourcePropertyValues.RemoveRange(values);

    public Task AddPropertyValueAsync(ResourcePropertyValue propertyValue, CancellationToken ct)
        => _dbContext.ResourcePropertyValues.AddAsync(propertyValue, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct)
        => _dbContext.SaveChangesAsync(ct);
}
