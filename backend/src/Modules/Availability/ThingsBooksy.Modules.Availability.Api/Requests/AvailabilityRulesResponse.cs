namespace ThingsBooksy.Modules.Availability.Api.Requests;

public sealed record AvailabilityRulesResponse(
    int BufferMinutes,
    IReadOnlyList<AvailabilityRuleDto> Rules,
    IReadOnlyList<AvailabilityRuleDto>? InheritedRules);
