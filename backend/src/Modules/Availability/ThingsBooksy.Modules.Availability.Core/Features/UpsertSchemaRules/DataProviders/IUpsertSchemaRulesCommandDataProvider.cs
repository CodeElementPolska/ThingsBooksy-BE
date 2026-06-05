using ThingsBooksy.Modules.Availability.Core.Domain;
using ThingsBooksy.Modules.Availability.Core.ReadModels;
using ThingsBooksy.Shared.Abstractions.DataProviders;

namespace ThingsBooksy.Modules.Availability.Core.Features.UpsertSchemaRules.DataProviders;

internal interface IUpsertSchemaRulesCommandDataProvider : IDataProvider
{
    Task<SchemaReadModel?> GetSchemaReadModelAsync(Guid schemaId, CancellationToken ct);
    Task<GroupReadModel?> GetGroupReadModelAsync(Guid groupId, CancellationToken ct);
    Task<SchemaRuleSet?> GetSchemaRuleSetAsync(Guid schemaId, CancellationToken ct);
    Task AddSchemaRuleSetAsync(SchemaRuleSet ruleSet, CancellationToken ct);
    Task RemoveRulesForSchemaAsync(Guid schemaRuleSetId, CancellationToken ct);
    Task AddRuleAsync(AvailabilityRule rule, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
