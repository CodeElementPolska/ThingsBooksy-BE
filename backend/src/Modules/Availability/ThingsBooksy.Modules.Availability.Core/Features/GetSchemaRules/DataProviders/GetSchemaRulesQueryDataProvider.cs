using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Availability.Core.DAL;
using ThingsBooksy.Modules.Availability.Core.Domain;
using ThingsBooksy.Modules.Availability.Core.ReadModels;

namespace ThingsBooksy.Modules.Availability.Core.Features.GetSchemaRules.DataProviders;

internal sealed class GetSchemaRulesQueryDataProvider : IGetSchemaRulesQueryDataProvider
{
    private readonly AvailabilityDbContext _dbContext;

    public GetSchemaRulesQueryDataProvider(AvailabilityDbContext dbContext)
        => _dbContext = dbContext;

    public Task<SchemaReadModel?> GetSchemaReadModelAsync(Guid schemaId, CancellationToken ct)
        => _dbContext.SchemaReadModels.FirstOrDefaultAsync(x => x.Id == schemaId, ct);

    public Task<GroupReadModel?> GetGroupReadModelAsync(Guid groupId, CancellationToken ct)
        => _dbContext.GroupReadModels.FirstOrDefaultAsync(x => x.Id == groupId, ct);

    public Task<SchemaRuleSet?> GetSchemaRuleSetAsync(Guid schemaId, CancellationToken ct)
        => _dbContext.SchemaRuleSets
            .Include(x => x.Rules)
            .FirstOrDefaultAsync(x => x.SchemaId == schemaId, ct);
}
