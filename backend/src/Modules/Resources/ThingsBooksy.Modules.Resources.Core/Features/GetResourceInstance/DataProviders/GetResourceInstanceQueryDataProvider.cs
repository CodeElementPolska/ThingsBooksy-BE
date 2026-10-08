using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.Features.GetResourceInstance.Models;

namespace ThingsBooksy.Modules.Resources.Core.Features.GetResourceInstance;

internal sealed class GetResourceInstanceQueryDataProvider : IGetResourceInstanceQueryDataProvider
{
    private readonly ResourcesDbContext _dbContext;

    public GetResourceInstanceQueryDataProvider(ResourcesDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<GetResourceInstanceQueryResult?> GetByIdAsync(Guid instanceId, CancellationToken ct)
    {
        var instance = await _dbContext.ResourceInstances
            .Include(x => x.PropertyValues)
            .FirstOrDefaultAsync(x => x.Id == instanceId, ct);

        if (instance is null)
            return null;

        var definitions = await _dbContext.ResourcePropertyDefinitions
            .Where(d => d.ResourceSchemaId == instance.ResourceSchemaId)
            .ToListAsync(ct);

        var defMap = definitions.ToDictionary(d => d.Id);

        var propertyValues = instance.PropertyValues
            .Select(pv =>
            {
                defMap.TryGetValue(pv.PropertyDefinitionId, out var def);
                return new PropertyValueResult(
                    pv.PropertyDefinitionId,
                    def?.Name ?? string.Empty,
                    def?.DataType.ToString() ?? string.Empty,
                    pv.Value);
            })
            .ToList();

        return new GetResourceInstanceQueryResult(
            instance.Id,
            instance.ResourceSchemaId,
            instance.GroupId,
            instance.Name,
            instance.Description,
            instance.OwnerId,
            instance.CreatedAt,
            instance.DeletedAt,
            propertyValues);
    }

    public Task<bool> IsOwnerAsync(Guid groupId, Guid userId, CancellationToken ct)
        => _dbContext.GroupReadModels.AnyAsync(g => g.Id == groupId && g.OwnerId == userId, ct);

    public Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct)
        => _dbContext.GroupMemberReadModels.AnyAsync(m => m.GroupId == groupId && m.UserId == userId, ct);
}
