using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.ReadModels;

namespace ThingsBooksy.Modules.Resources.Core.Features.UpdateResourceSchema;

internal sealed class UpdateResourceSchemaCommandDataProvider : IUpdateResourceSchemaCommandDataProvider
{
    private readonly ResourcesDbContext _dbContext;

    public UpdateResourceSchemaCommandDataProvider(ResourcesDbContext dbContext)
        => _dbContext = dbContext;

    public Task<ResourceSchema?> GetResourceSchemaWithDefinitionsAsync(Guid typeId, CancellationToken ct)
        => _dbContext.ResourceSchemas
            .Include(t => t.PropertyDefinitions)
            .FirstOrDefaultAsync(t => t.Id == typeId, ct);

    public Task<bool> ExistsByGroupAndNameAsync(Guid groupId, string normalizedName, Guid? excludeId, CancellationToken ct)
        => _dbContext.ResourceSchemas.IgnoreQueryFilters().AnyAsync(
            t => t.GroupId == groupId && t.Name == normalizedName && (excludeId == null || t.Id != excludeId.Value) && t.DeletedAt == null,
            ct);

    public Task<GroupReadModel?> GetGroupAsync(Guid groupId, CancellationToken ct)
        => _dbContext.GroupReadModels.FirstOrDefaultAsync(g => g.Id == groupId, ct);

    public void RemovePropertyDefinitions(IEnumerable<ResourcePropertyDefinition> definitions)
        => _dbContext.ResourcePropertyDefinitions.RemoveRange(definitions);

    public Task AddPropertyDefinitionAsync(ResourcePropertyDefinition definition, CancellationToken ct)
        => _dbContext.ResourcePropertyDefinitions.AddAsync(definition, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct)
        => _dbContext.SaveChangesAsync(ct);
}
