using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Availability.Core.DAL;
using ThingsBooksy.Modules.Availability.Core.Domain;
using ThingsBooksy.Modules.Availability.Core.ReadModels;

namespace ThingsBooksy.Modules.Availability.Core.Features.UpsertResourceRules.DataProviders;

internal sealed class UpsertResourceRulesCommandDataProvider : IUpsertResourceRulesCommandDataProvider
{
    private readonly AvailabilityDbContext _dbContext;

    public UpsertResourceRulesCommandDataProvider(AvailabilityDbContext dbContext)
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

    public Task AddResourceRuleSetAsync(ResourceRuleSet ruleSet, CancellationToken ct)
        => _dbContext.ResourceRuleSets.AddAsync(ruleSet, ct).AsTask();

    public async Task RemoveRulesForResourceAsync(Guid resourceRuleSetId, CancellationToken ct)
    {
        var rules = await _dbContext.AvailabilityRules
            .Where(x => x.ResourceRuleSetId == resourceRuleSetId)
            .ToListAsync(ct);
        _dbContext.AvailabilityRules.RemoveRange(rules);
    }

    public Task AddRuleAsync(AvailabilityRule rule, CancellationToken ct)
        => _dbContext.AvailabilityRules.AddAsync(rule, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct)
        => _dbContext.SaveChangesAsync(ct);
}
