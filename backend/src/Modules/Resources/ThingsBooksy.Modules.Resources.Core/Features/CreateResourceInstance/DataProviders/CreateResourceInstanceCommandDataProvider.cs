using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.ReadModels;

namespace ThingsBooksy.Modules.Resources.Core.Features.CreateResourceInstance;

internal sealed class CreateResourceInstanceCommandDataProvider : ICreateResourceInstanceCommandDataProvider
{
    private readonly ResourcesDbContext _dbContext;

    public CreateResourceInstanceCommandDataProvider(ResourcesDbContext dbContext)
        => _dbContext = dbContext;

    public Task<ResourceSchema?> GetResourceSchemaAsync(Guid resourceSchemaId, CancellationToken ct)
        => _dbContext.ResourceSchemas.FirstOrDefaultAsync(t => t.Id == resourceSchemaId, ct);

    public Task<GroupReadModel?> GetGroupAsync(Guid groupId, CancellationToken ct)
        => _dbContext.GroupReadModels.FirstOrDefaultAsync(g => g.Id == groupId, ct);

    public Task<bool> NameExistsAsync(Guid resourceSchemaId, string name, CancellationToken ct)
        => _dbContext.ResourceInstances.AnyAsync(x => x.ResourceSchemaId == resourceSchemaId && x.Name == name, ct);

    public Task<List<ResourcePropertyDefinition>> GetPropertyDefinitionsAsync(Guid resourceSchemaId, CancellationToken ct)
        => _dbContext.ResourcePropertyDefinitions.Where(d => d.ResourceSchemaId == resourceSchemaId).ToListAsync(ct);

    public Task AddResourceInstanceAsync(ResourceInstance instance, CancellationToken ct)
        => _dbContext.ResourceInstances.AddAsync(instance, ct).AsTask();

    public Task AddPropertyValueAsync(ResourcePropertyValue propertyValue, CancellationToken ct)
        => _dbContext.ResourcePropertyValues.AddAsync(propertyValue, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct)
        => _dbContext.SaveChangesAsync(ct);
}
