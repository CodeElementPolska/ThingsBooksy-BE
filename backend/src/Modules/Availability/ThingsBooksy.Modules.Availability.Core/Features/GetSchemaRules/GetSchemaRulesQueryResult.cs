using ThingsBooksy.Modules.Availability.Core.Features.GetSchemaRules.Models;

namespace ThingsBooksy.Modules.Availability.Core.Features.GetSchemaRules;

internal record GetSchemaRulesQueryResult(
    int BufferMinutes,
    IReadOnlyList<AvailabilityRuleResult> Rules,
    IReadOnlyList<AvailabilityRuleResult>? InheritedRules);
