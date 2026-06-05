namespace ThingsBooksy.Modules.Availability.Api.Requests;

public sealed record AvailabilityRuleDto(
    Guid RuleId,
    string RuleType,
    string RuleMode,
    int[]? DaysOfWeek,
    string StartTime,
    string EndTime,
    int EndDayOffset,
    string? StartDate,
    string? EndDate);
