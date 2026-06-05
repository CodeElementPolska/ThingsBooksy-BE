using ThingsBooksy.Shared.Abstractions.Queries;

namespace ThingsBooksy.Modules.Availability.Core.Features.GetSchemaRules;

internal record GetSchemaRulesQuery(Guid SchemaId, Guid CallerId) : IQuery<GetSchemaRulesQueryResult>;
