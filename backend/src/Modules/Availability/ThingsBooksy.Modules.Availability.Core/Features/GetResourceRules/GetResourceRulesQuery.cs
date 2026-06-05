using ThingsBooksy.Shared.Abstractions.Queries;

namespace ThingsBooksy.Modules.Availability.Core.Features.GetResourceRules;

internal record GetResourceRulesQuery(Guid ResourceId, Guid CallerId) : IQuery<GetResourceRulesQueryResult>;
