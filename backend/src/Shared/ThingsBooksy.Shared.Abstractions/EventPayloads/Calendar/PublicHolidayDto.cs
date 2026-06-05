namespace ThingsBooksy.Shared.Abstractions.EventPayloads.Calendar;

public record PublicHolidayDto(DateOnly Date, string LocalName, string Name);
