using ThingsBooksy.Modules.Availability.Core.Domain;
using ThingsBooksy.Modules.Availability.Core.ReadModels;
using ThingsBooksy.Shared.Abstractions.DataProviders;

namespace ThingsBooksy.Modules.Availability.Core.Features.GetResourceRules.DataProviders;

internal interface IGetResourceRulesQueryDataProvider : IDataProvider
{
    Task<ResourceReadModel?> GetResourceReadModelAsync(Guid resourceId, CancellationToken ct);
    Task<SchemaReadModel?> GetSchemaReadModelAsync(Guid schemaId, CancellationToken ct);
    Task<GroupReadModel?> GetGroupReadModelAsync(Guid groupId, CancellationToken ct);
    Task<ResourceRuleSet?> GetResourceRuleSetAsync(Guid resourceId, CancellationToken ct);
    Task<SchemaRuleSet?> GetSchemaRuleSetAsync(Guid schemaId, CancellationToken ct);
}
