namespace ThingsBooksy.Modules.Availability.Core.Features.GetResourceRules.Models;

internal record ResourceAvailabilityRuleResult(
    Guid RuleId,
    string RuleType,
    string RuleMode,
    int[]? DaysOfWeek,
    string StartTime,
    string EndTime,
    int EndDayOffset,
    string? StartDate,
    string? EndDate);
