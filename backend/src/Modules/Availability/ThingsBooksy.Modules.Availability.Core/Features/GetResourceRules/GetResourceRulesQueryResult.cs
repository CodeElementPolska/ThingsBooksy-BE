using ThingsBooksy.Modules.Availability.Core.Features.GetResourceRules.Models;

namespace ThingsBooksy.Modules.Availability.Core.Features.GetResourceRules;

internal record GetResourceRulesQueryResult(
    int BufferMinutes,
    IReadOnlyList<ResourceAvailabilityRuleResult> Rules,
    IReadOnlyList<ResourceAvailabilityRuleResult>? InheritedRules);
