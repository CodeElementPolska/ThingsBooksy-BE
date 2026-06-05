using ThingsBooksy.Shared.Abstractions.EventPayloads.Calendar;

namespace ThingsBooksy.Shared.Abstractions.Events.Calendar;

public record HolidaysRefreshedEvent(int Year, IReadOnlyList<PublicHolidayDto> Holidays) : IEvent;
