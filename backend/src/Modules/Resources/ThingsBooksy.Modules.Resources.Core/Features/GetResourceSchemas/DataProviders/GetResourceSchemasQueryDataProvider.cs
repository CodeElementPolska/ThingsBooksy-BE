using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Features.GetResourceSchema.Models;

namespace ThingsBooksy.Modules.Resources.Core.Features.GetResourceSchemas;

internal sealed class GetResourceSchemasQueryDataProvider : IGetResourceSchemasQueryDataProvider
{
    private readonly ResourcesDbContext _dbContext;

    public GetResourceSchemasQueryDataProvider(ResourcesDbContext dbContext)
        => _dbContext = dbContext;

    public Task<bool> IsOwnerAsync(Guid groupId, Guid userId, CancellationToken ct)
        => _dbContext.GroupReadModels.AnyAsync(g => g.Id == groupId && g.OwnerId == userId, ct);

    public Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct)
        => _dbContext.GroupMemberReadModels.AnyAsync(m => m.GroupId == groupId && m.UserId == userId, ct);

    public Task<List<GetResourceSchemasQueryResult>> GetByGroupIdAsync(Guid groupId, CancellationToken ct)
        => (from rt in _dbContext.ResourceSchemas.Include(x => x.PropertyDefinitions)
            where rt.GroupId == groupId
            select new GetResourceSchemasQueryResult(
                rt.Id,
                rt.GroupId,
                rt.Name,
                rt.Description,
                rt.CreatedAt,
                rt.PropertyDefinitions
                    .Select(d => new PropertyDefinitionResult(d.Id, d.Name, d.DataType.ToString(), d.IsRequired))
                    .ToList()))
           .ToListAsync(ct);
}
