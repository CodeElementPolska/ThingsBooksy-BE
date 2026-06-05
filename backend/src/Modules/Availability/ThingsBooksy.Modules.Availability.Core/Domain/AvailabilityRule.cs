namespace ThingsBooksy.Modules.Availability.Core.Domain;

internal class AvailabilityRule
{
    public Guid Id { get; private set; }
    public Guid? SchemaRuleSetId { get; private set; }
    public Guid? ResourceRuleSetId { get; private set; }
    public RuleType RuleType { get; private set; }
    public RuleMode RuleMode { get; private set; }
    public int[]? DaysOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int EndDayOffset { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }

    private AvailabilityRule() { }

    public static AvailabilityRule CreateForSchemaRuleSet(
        Guid schemaRuleSetId,
        RuleType ruleType,
        RuleMode ruleMode,
        int[]? daysOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly? startDate,
        DateOnly? endDate)
        => new()
        {
            Id = Guid.CreateVersion7(),
            SchemaRuleSetId = schemaRuleSetId,
            RuleType = ruleType,
            RuleMode = ruleMode,
            DaysOfWeek = daysOfWeek,
            StartTime = startTime,
            EndTime = endTime,
            EndDayOffset = endTime < startTime ? 1 : 0,
            StartDate = startDate,
            EndDate = endDate
        };

    public static AvailabilityRule CreateForResourceRuleSet(
        Guid resourceRuleSetId,
        RuleType ruleType,
        RuleMode ruleMode,
        int[]? daysOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly? startDate,
        DateOnly? endDate)
        => new()
        {
            Id = Guid.CreateVersion7(),
            ResourceRuleSetId = resourceRuleSetId,
            RuleType = ruleType,
            RuleMode = ruleMode,
            DaysOfWeek = daysOfWeek,
            StartTime = startTime,
            EndTime = endTime,
            EndDayOffset = endTime < startTime ? 1 : 0,
            StartDate = startDate,
            EndDate = endDate
        };
}
