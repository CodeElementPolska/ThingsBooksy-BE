using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Availability.Core.DAL;
using ThingsBooksy.Modules.Availability.Core.Domain;
using ThingsBooksy.Modules.Availability.Core.ReadModels;

namespace ThingsBooksy.Modules.Availability.Core.Features.GetResourceRules.DataProviders;

internal sealed class GetResourceRulesQueryDataProvider : IGetResourceRulesQueryDataProvider
{
    private readonly AvailabilityDbContext _dbContext;

    public GetResourceRulesQueryDataProvider(AvailabilityDbContext dbContext)
        => _dbContext = dbContext;

    public Task<ResourceReadModel?> GetResourceReadModelAsync(Guid resourceId, CancellationToken ct)
        => _dbContext.ResourceReadModels.FirstOrDefaultAsync(x => x.Id == resourceId, ct);

    public Task<SchemaReadModel?> GetSchemaReadModelAsync(Guid schemaId, CancellationToken ct)
        => _dbContext.SchemaReadModels.FirstOrDefaultAsync(x => x.Id == schemaId, ct);

    public Task<GroupReadModel?> GetGroupReadModelAsync(Guid groupId, CancellationToken ct)
        => _dbContext.GroupReadModels.FirstOrDefaultAsync(x => x.Id == groupId, ct);

    public Task<ResourceRuleSet?> GetResourceRuleSetAsync(Guid resourceId, CancellationToken ct)
        => _dbContext.ResourceRuleSets
            .Include(x => x.Rules)
            .FirstOrDefaultAsync(x => x.ResourceId == resourceId, ct);

    public Task<SchemaRuleSet?> GetSchemaRuleSetAsync(Guid schemaId, CancellationToken ct)
        => _dbContext.SchemaRuleSets
            .Include(x => x.Rules)
            .FirstOrDefaultAsync(x => x.SchemaId == schemaId, ct);
}
