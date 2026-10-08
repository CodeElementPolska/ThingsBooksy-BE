using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;

namespace ThingsBooksy.Modules.Resources.Core.Features.GetResourceInstances;

internal sealed class GetResourceInstancesQueryDataProvider : IGetResourceInstancesQueryDataProvider
{
    private readonly ResourcesDbContext _dbContext;

    public GetResourceInstancesQueryDataProvider(ResourcesDbContext dbContext)
        => _dbContext = dbContext;

    public Task<bool> IsOwnerAsync(Guid groupId, Guid userId, CancellationToken ct)
        => _dbContext.GroupReadModels.AnyAsync(g => g.Id == groupId && g.OwnerId == userId, ct);

    public Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct)
        => _dbContext.GroupMemberReadModels.AnyAsync(m => m.GroupId == groupId && m.UserId == userId, ct);

    public Task<ResourceSchema?> GetResourceSchemaAsync(Guid resourceSchemaId, CancellationToken ct)
        => _dbContext.ResourceSchemas.FirstOrDefaultAsync(t => t.Id == resourceSchemaId, ct);

    public async Task<List<ResourceInstance>> GetInstancesAsync(Guid? resourceSchemaId, Guid? groupId, bool includeDeleted, Guid? afterId, int take, CancellationToken ct)
    {
        IQueryable<ResourceInstance> query = includeDeleted
            ? _dbContext.ResourceInstances.IgnoreQueryFilters().Include(x => x.PropertyValues)
            : _dbContext.ResourceInstances.Include(x => x.PropertyValues);

        if (resourceSchemaId.HasValue)
            query = query.Where(x => x.ResourceSchemaId == resourceSchemaId.Value);

        if (groupId.HasValue)
            query = query.Where(x => x.GroupId == groupId.Value);

        if (afterId.HasValue)
            query = query.Where(x => x.Id > afterId.Value);

        return await query.OrderBy(x => x.Id).Take(take).ToListAsync(ct);
    }

    public Task<List<ResourcePropertyDefinition>> GetPropertyDefinitionsAsync(IEnumerable<Guid> resourceSchemaIds, CancellationToken ct)
        => _dbContext.ResourcePropertyDefinitions
            .Where(d => resourceSchemaIds.Contains(d.ResourceSchemaId))
            .ToListAsync(ct);
}
