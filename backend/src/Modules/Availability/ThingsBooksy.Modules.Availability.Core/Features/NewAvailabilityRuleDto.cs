namespace ThingsBooksy.Modules.Availability.Core.Features;

internal record NewAvailabilityRuleDto(
    string RuleType,
    string RuleMode,
    int[]? DaysOfWeek,
    string StartTime,
    string EndTime,
    string? StartDate,
    string? EndDate);
