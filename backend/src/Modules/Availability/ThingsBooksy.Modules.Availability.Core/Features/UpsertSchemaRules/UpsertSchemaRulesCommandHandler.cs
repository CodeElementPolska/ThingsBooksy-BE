using ThingsBooksy.Modules.Availability.Core.Domain;
using ThingsBooksy.Modules.Availability.Core.Exceptions;
using ThingsBooksy.Modules.Availability.Core.Features.UpsertSchemaRules.DataProviders;
using ThingsBooksy.Shared.Abstractions.Commands;

namespace ThingsBooksy.Modules.Availability.Core.Features.UpsertSchemaRules;

internal sealed class UpsertSchemaRulesCommandHandler : ICommandHandler<UpsertSchemaRulesCommand>
{
    private readonly IUpsertSchemaRulesCommandDataProvider _dataProvider;

    public UpsertSchemaRulesCommandHandler(IUpsertSchemaRulesCommandDataProvider dataProvider)
        => _dataProvider = dataProvider;

    public async Task HandleAsync(UpsertSchemaRulesCommand command, CancellationToken cancellationToken = default)
    {
        var schema = await _dataProvider.GetSchemaReadModelAsync(command.SchemaId, cancellationToken);
        if (schema is null)
            throw new AvailabilityNotFoundException($"Schema '{command.SchemaId}' not found.");

        var group = await _dataProvider.GetGroupReadModelAsync(schema.GroupId, cancellationToken);
        if (group is null)
            throw new AvailabilityNotFoundException($"Group '{schema.GroupId}' not found.");

        if (group.OwnerId != command.CallerId)
            throw new AvailabilityForbiddenException("Access to this schema's rules is forbidden.");

        if (command.BufferMinutes < 0 || command.BufferMinutes > 1440)
            throw new AvailabilityDomainException("BufferMinutes must be between 0 and 1440.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var dto in command.Rules)
        {
            if (Enum.Parse<RuleType>(dto.RuleType, ignoreCase: true) == RuleType.OneOff)
            {
                if (dto.StartDate is null || !DateOnly.TryParse(dto.StartDate, out var startDate))
                    throw new AvailabilityDomainException("OneOff rules must have a valid StartDate.");

                if (startDate < today)
                    throw new AvailabilityDomainException("OneOff rule StartDate must be today or in the future.");
            }
        }

        ValidateNoAvailableOverlaps(command.Rules);

        var ruleSet = await _dataProvider.GetSchemaRuleSetAsync(command.SchemaId, cancellationToken);
        if (ruleSet is null)
        {
            ruleSet = SchemaRuleSet.Create(command.SchemaId, schema.GroupId, command.BufferMinutes);
            await _dataProvider.AddSchemaRuleSetAsync(ruleSet, cancellationToken);
        }
        else
        {
            ruleSet.UpdateBuffer(command.BufferMinutes);
            await _dataProvider.RemoveRulesForSchemaAsync(ruleSet.Id, cancellationToken);
        }

        foreach (var dto in command.Rules)
        {
            var ruleType = Enum.Parse<RuleType>(dto.RuleType, ignoreCase: true);
            var ruleMode = Enum.Parse<RuleMode>(dto.RuleMode, ignoreCase: true);
            var startTime = TimeOnly.Parse(dto.StartTime);
            var endTime = TimeOnly.Parse(dto.EndTime);
            DateOnly? startDate = dto.StartDate is not null ? DateOnly.Parse(dto.StartDate) : null;
            DateOnly? endDate = dto.EndDate is not null ? DateOnly.Parse(dto.EndDate) : null;

            var rule = AvailabilityRule.CreateForSchemaRuleSet(
                ruleSet.Id, ruleType, ruleMode, dto.DaysOfWeek, startTime, endTime, startDate, endDate);

            await _dataProvider.AddRuleAsync(rule, cancellationToken);
        }

        await _dataProvider.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateNoAvailableOverlaps(IReadOnlyList<NewAvailabilityRuleDto> rules)
    {
        var available = rules
            .Where(r => string.Equals(r.RuleMode, "Available", StringComparison.OrdinalIgnoreCase))
            .ToList();

        for (var i = 0; i < available.Count; i++)
        {
            for (var j = i + 1; j < available.Count; j++)
            {
                if (DoRulesOverlap(available[i], available[j]))
                    throw new AvailabilityConflictException("Two or more Available rules have overlapping time windows.");
            }
        }
    }

    private static bool DoRulesOverlap(NewAvailabilityRuleDto a, NewAvailabilityRuleDto b)
    {
        var typeA = Enum.Parse<RuleType>(a.RuleType, ignoreCase: true);
        var typeB = Enum.Parse<RuleType>(b.RuleType, ignoreCase: true);

        if (typeA != typeB)
            return false;

        var startA = TimeOnly.Parse(a.StartTime);
        var endA = TimeOnly.Parse(a.EndTime);
        var endDayOffsetA = endA < startA ? 1 : 0;

        var startB = TimeOnly.Parse(b.StartTime);
        var endB = TimeOnly.Parse(b.EndTime);
        var endDayOffsetB = endB < startB ? 1 : 0;

        if (typeA == RuleType.Recurring)
        {
            if (a.DaysOfWeek is null || b.DaysOfWeek is null)
                return false;

            var sharedDays = a.DaysOfWeek.Intersect(b.DaysOfWeek).Any();
            if (!sharedDays)
                return false;

            return TimeWindowsOverlap(startA, endA, endDayOffsetA, startB, endB, endDayOffsetB);
        }

        if (typeA == RuleType.OneOff)
        {
            if (a.StartDate is null || b.StartDate is null)
                return false;

            var startDateA = DateOnly.Parse(a.StartDate);
            var endDateA = a.EndDate is not null ? DateOnly.Parse(a.EndDate) : startDateA;
            var startDateB = DateOnly.Parse(b.StartDate);
            var endDateB = b.EndDate is not null ? DateOnly.Parse(b.EndDate) : startDateB;

            if (startDateA > endDateB || startDateB > endDateA)
                return false;

            return TimeWindowsOverlap(startA, endA, endDayOffsetA, startB, endB, endDayOffsetB);
        }

        return false;
    }

    private static bool TimeWindowsOverlap(
        TimeOnly startA, TimeOnly endA, int offsetA,
        TimeOnly startB, TimeOnly endB, int offsetB)
    {
        // Convert to minute-based values for easy comparison
        var startMinA = startA.Hour * 60 + startA.Minute;
        var endMinA = endA.Hour * 60 + endA.Minute + offsetA * 1440;
        var startMinB = startB.Hour * 60 + startB.Minute;
        var endMinB = endB.Hour * 60 + endB.Minute + offsetB * 1440;

        return startMinA < endMinB && startMinB < endMinA;
    }
}
