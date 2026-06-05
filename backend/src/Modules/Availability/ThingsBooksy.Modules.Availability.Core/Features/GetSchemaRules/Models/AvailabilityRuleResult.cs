namespace ThingsBooksy.Modules.Availability.Core.Features.GetSchemaRules.Models;

internal record AvailabilityRuleResult(
    Guid RuleId,
    string RuleType,
    string RuleMode,
    int[]? DaysOfWeek,
    string StartTime,
    string EndTime,
    int EndDayOffset,
    string? StartDate,
    string? EndDate);
