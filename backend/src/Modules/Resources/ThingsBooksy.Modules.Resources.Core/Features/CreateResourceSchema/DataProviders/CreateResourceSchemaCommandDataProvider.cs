using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.ReadModels;

namespace ThingsBooksy.Modules.Resources.Core.Features.CreateResourceSchema;

internal sealed class CreateResourceSchemaCommandDataProvider : ICreateResourceSchemaCommandDataProvider
{
    private readonly ResourcesDbContext _dbContext;

    public CreateResourceSchemaCommandDataProvider(ResourcesDbContext dbContext)
        => _dbContext = dbContext;

    public Task<GroupReadModel?> GetGroupAsync(Guid groupId, CancellationToken ct)
        => _dbContext.GroupReadModels.FirstOrDefaultAsync(g => g.Id == groupId, ct);

    public Task<bool> ExistsByGroupAndNameAsync(Guid groupId, string normalizedName, Guid? excludeId, CancellationToken ct)
        => _dbContext.ResourceSchemas.IgnoreQueryFilters().AnyAsync(
            t => t.GroupId == groupId && t.Name == normalizedName && (excludeId == null || t.Id != excludeId.Value) && t.DeletedAt == null,
            ct);

    public Task AddResourceSchemaAsync(ResourceSchema resourceSchema, CancellationToken ct)
        => _dbContext.ResourceSchemas.AddAsync(resourceSchema, ct).AsTask();

    public Task AddPropertyDefinitionAsync(ResourcePropertyDefinition definition, CancellationToken ct)
        => _dbContext.ResourcePropertyDefinitions.AddAsync(definition, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct)
        => _dbContext.SaveChangesAsync(ct);
}
