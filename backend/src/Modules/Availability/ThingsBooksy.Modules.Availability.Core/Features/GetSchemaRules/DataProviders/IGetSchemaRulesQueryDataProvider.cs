using ThingsBooksy.Modules.Availability.Core.Domain;
using ThingsBooksy.Modules.Availability.Core.ReadModels;
using ThingsBooksy.Shared.Abstractions.DataProviders;

namespace ThingsBooksy.Modules.Availability.Core.Features.GetSchemaRules.DataProviders;

internal interface IGetSchemaRulesQueryDataProvider : IDataProvider
{
    Task<SchemaReadModel?> GetSchemaReadModelAsync(Guid schemaId, CancellationToken ct);
    Task<GroupReadModel?> GetGroupReadModelAsync(Guid groupId, CancellationToken ct);
    Task<SchemaRuleSet?> GetSchemaRuleSetAsync(Guid schemaId, CancellationToken ct);
}
