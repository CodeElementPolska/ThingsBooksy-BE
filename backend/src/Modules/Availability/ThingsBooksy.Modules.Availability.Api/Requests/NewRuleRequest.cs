using System.ComponentModel.DataAnnotations;

namespace ThingsBooksy.Modules.Availability.Api.Requests;

public record NewRuleRequest(
    [AllowedValues("RECURRING", "ONE_OFF")] string RuleType,
    [AllowedValues("AVAILABLE", "UNAVAILABLE")] string RuleMode,
    int[]? DaysOfWeek,
    string StartTime,
    string EndTime,
    string? StartDate,
    string? EndDate);
