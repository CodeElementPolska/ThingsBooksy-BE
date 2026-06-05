using ThingsBooksy.Modules.Availability.Core.Domain;
using ThingsBooksy.Modules.Availability.Core.Exceptions;
using ThingsBooksy.Modules.Availability.Core.Features.GetSchemaRules.DataProviders;
using ThingsBooksy.Modules.Availability.Core.Features.GetSchemaRules.Models;
using ThingsBooksy.Shared.Abstractions.Queries;

namespace ThingsBooksy.Modules.Availability.Core.Features.GetSchemaRules;

internal sealed class GetSchemaRulesQueryHandler : IQueryHandler<GetSchemaRulesQuery, GetSchemaRulesQueryResult>
{
    private readonly IGetSchemaRulesQueryDataProvider _dataProvider;

    public GetSchemaRulesQueryHandler(IGetSchemaRulesQueryDataProvider dataProvider)
        => _dataProvider = dataProvider;

    public async Task<GetSchemaRulesQueryResult> HandleAsync(GetSchemaRulesQuery query, CancellationToken cancellationToken = default)
    {
        var schema = await _dataProvider.GetSchemaReadModelAsync(query.SchemaId, cancellationToken);
        if (schema is null)
            throw new AvailabilityNotFoundException($"Schema '{query.SchemaId}' not found.");

        var group = await _dataProvider.GetGroupReadModelAsync(schema.GroupId, cancellationToken);
        if (group is null)
            throw new AvailabilityNotFoundException($"Group '{schema.GroupId}' not found.");

        if (group.OwnerId != query.CallerId)
            throw new AvailabilityForbiddenException("Access to this schema's rules is forbidden.");

        var ruleSet = await _dataProvider.GetSchemaRuleSetAsync(query.SchemaId, cancellationToken);

        if (ruleSet is null)
            return new GetSchemaRulesQueryResult(schema.DefaultBufferMinutes, [], null);

        var rules = ruleSet.Rules.Select(MapRule).ToList();

        return new GetSchemaRulesQueryResult(ruleSet.BufferMinutes, rules, null);
    }

    private static AvailabilityRuleResult MapRule(AvailabilityRule rule)
        => new(
            rule.Id,
            rule.RuleType.ToString(),
            rule.RuleMode.ToString(),
            rule.DaysOfWeek,
            rule.StartTime.ToString("HH:mm"),
            rule.EndTime.ToString("HH:mm"),
            rule.EndDayOffset,
            rule.StartDate?.ToString("yyyy-MM-dd"),
            rule.EndDate?.ToString("yyyy-MM-dd"));
}
