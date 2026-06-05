namespace ThingsBooksy.Modules.Calendar.Core.Domain;

internal class PublicHolidayFetchLog
{
    public int Year { get; private set; }
    public DateTime FetchedAt { get; private set; }
    public int HolidayCount { get; private set; }

#pragma warning disable CS8618
    private PublicHolidayFetchLog() { }
#pragma warning restore CS8618

    public static PublicHolidayFetchLog Create(int year, DateTime now, int count)
        => new() { Year = year, FetchedAt = now, HolidayCount = count };
}
