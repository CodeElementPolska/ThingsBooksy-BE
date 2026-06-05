using ThingsBooksy.Modules.Availability.Core.Domain;
using ThingsBooksy.Modules.Availability.Core.ReadModels;
using ThingsBooksy.Shared.Abstractions.DataProviders;

namespace ThingsBooksy.Modules.Availability.Core.Features.UpsertResourceRules.DataProviders;

internal interface IUpsertResourceRulesCommandDataProvider : IDataProvider
{
    Task<ResourceReadModel?> GetResourceReadModelAsync(Guid resourceId, CancellationToken ct);
    Task<SchemaReadModel?> GetSchemaReadModelAsync(Guid schemaId, CancellationToken ct);
    Task<GroupReadModel?> GetGroupReadModelAsync(Guid groupId, CancellationToken ct);
    Task<ResourceRuleSet?> GetResourceRuleSetAsync(Guid resourceId, CancellationToken ct);
    Task AddResourceRuleSetAsync(ResourceRuleSet ruleSet, CancellationToken ct);
    Task RemoveRulesForResourceAsync(Guid resourceRuleSetId, CancellationToken ct);
    Task AddRuleAsync(AvailabilityRule rule, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
