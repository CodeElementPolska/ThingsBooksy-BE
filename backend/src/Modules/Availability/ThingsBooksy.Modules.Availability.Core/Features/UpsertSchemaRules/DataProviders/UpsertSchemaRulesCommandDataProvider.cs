using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Availability.Core.DAL;
using ThingsBooksy.Modules.Availability.Core.Domain;
using ThingsBooksy.Modules.Availability.Core.ReadModels;

namespace ThingsBooksy.Modules.Availability.Core.Features.UpsertSchemaRules.DataProviders;

internal sealed class UpsertSchemaRulesCommandDataProvider : IUpsertSchemaRulesCommandDataProvider
{
    private readonly AvailabilityDbContext _dbContext;

    public UpsertSchemaRulesCommandDataProvider(AvailabilityDbContext dbContext)
        => _dbContext = dbContext;

    public Task<SchemaReadModel?> GetSchemaReadModelAsync(Guid schemaId, CancellationToken ct)
        => _dbContext.SchemaReadModels.FirstOrDefaultAsync(x => x.Id == schemaId, ct);

    public Task<GroupReadModel?> GetGroupReadModelAsync(Guid groupId, CancellationToken ct)
        => _dbContext.GroupReadModels.FirstOrDefaultAsync(x => x.Id == groupId, ct);

    public Task<SchemaRuleSet?> GetSchemaRuleSetAsync(Guid schemaId, CancellationToken ct)
        => _dbContext.SchemaRuleSets
            .Include(x => x.Rules)
            .FirstOrDefaultAsync(x => x.SchemaId == schemaId, ct);

    public Task AddSchemaRuleSetAsync(SchemaRuleSet ruleSet, CancellationToken ct)
        => _dbContext.SchemaRuleSets.AddAsync(ruleSet, ct).AsTask();

    public async Task RemoveRulesForSchemaAsync(Guid schemaRuleSetId, CancellationToken ct)
    {
        var rules = await _dbContext.AvailabilityRules
            .Where(x => x.SchemaRuleSetId == schemaRuleSetId)
            .ToListAsync(ct);
        _dbContext.AvailabilityRules.RemoveRange(rules);
    }

    public Task AddRuleAsync(AvailabilityRule rule, CancellationToken ct)
        => _dbContext.AvailabilityRules.AddAsync(rule, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct)
        => _dbContext.SaveChangesAsync(ct);
}
