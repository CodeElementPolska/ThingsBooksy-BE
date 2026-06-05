using ThingsBooksy.Modules.Availability.Core.Domain;
using ThingsBooksy.Modules.Availability.Core.Exceptions;
using ThingsBooksy.Modules.Availability.Core.Features.GetResourceRules.DataProviders;
using ThingsBooksy.Modules.Availability.Core.Features.GetResourceRules.Models;
using ThingsBooksy.Shared.Abstractions.Queries;

namespace ThingsBooksy.Modules.Availability.Core.Features.GetResourceRules;

internal sealed class GetResourceRulesQueryHandler : IQueryHandler<GetResourceRulesQuery, GetResourceRulesQueryResult>
{
    private readonly IGetResourceRulesQueryDataProvider _dataProvider;

    public GetResourceRulesQueryHandler(IGetResourceRulesQueryDataProvider dataProvider)
        => _dataProvider = dataProvider;

    public async Task<GetResourceRulesQueryResult> HandleAsync(GetResourceRulesQuery query, CancellationToken cancellationToken = default)
    {
        var resource = await _dataProvider.GetResourceReadModelAsync(query.ResourceId, cancellationToken);
        if (resource is null)
            throw new AvailabilityNotFoundException($"Resource '{query.ResourceId}' not found.");

        var schema = await _dataProvider.GetSchemaReadModelAsync(resource.SchemaId, cancellationToken);
        if (schema is null)
            throw new AvailabilityNotFoundException($"Schema '{resource.SchemaId}' not found.");

        var group = await _dataProvider.GetGroupReadModelAsync(schema.GroupId, cancellationToken);
        if (group is null)
            throw new AvailabilityNotFoundException($"Group '{schema.GroupId}' not found.");

        if (group.OwnerId != query.CallerId)
            throw new AvailabilityForbiddenException("Access to this resource's rules is forbidden.");

        var resourceRuleSet = await _dataProvider.GetResourceRuleSetAsync(query.ResourceId, cancellationToken);
        var schemaRuleSet = await _dataProvider.GetSchemaRuleSetAsync(resource.SchemaId, cancellationToken);

        var ownRules = resourceRuleSet?.Rules.Select(MapRule).ToList() ?? [];
        var inheritedRules = schemaRuleSet?.Rules.Select(MapRule).ToList();

        var effectiveBuffer = resourceRuleSet?.BufferMinutesOverride
            ?? schemaRuleSet?.BufferMinutes
            ?? schema.DefaultBufferMinutes;

        return new GetResourceRulesQueryResult(effectiveBuffer, ownRules, inheritedRules);
    }

    private static ResourceAvailabilityRuleResult MapRule(AvailabilityRule rule)
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
