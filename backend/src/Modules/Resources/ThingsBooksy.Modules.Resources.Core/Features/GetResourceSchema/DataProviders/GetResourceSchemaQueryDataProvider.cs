using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Features.GetResourceSchema.Models;

namespace ThingsBooksy.Modules.Resources.Core.Features.GetResourceSchema;

internal sealed class GetResourceSchemaQueryDataProvider : IGetResourceSchemaQueryDataProvider
{
    private readonly ResourcesDbContext _dbContext;

    public GetResourceSchemaQueryDataProvider(ResourcesDbContext dbContext)
        => _dbContext = dbContext;

    public Task<GetResourceSchemaQueryResult?> GetByIdAsync(Guid typeId, CancellationToken ct)
        => (from rt in _dbContext.ResourceSchemas.Include(x => x.PropertyDefinitions)
            where rt.Id == typeId
            select new GetResourceSchemaQueryResult(
                rt.Id,
                rt.GroupId,
                rt.Name,
                rt.Description,
                rt.CreatedAt,
                rt.PropertyDefinitions
                    .Select(d => new PropertyDefinitionResult(d.Id, d.Name, d.DataType.ToString(), d.IsRequired))
                    .ToList()))
           .FirstOrDefaultAsync(ct);

    public Task<bool> IsOwnerAsync(Guid groupId, Guid userId, CancellationToken ct)
        => _dbContext.GroupReadModels.AnyAsync(g => g.Id == groupId && g.OwnerId == userId, ct);

    public Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct)
        => _dbContext.GroupMemberReadModels.AnyAsync(m => m.GroupId == groupId && m.UserId == userId, ct);
}
