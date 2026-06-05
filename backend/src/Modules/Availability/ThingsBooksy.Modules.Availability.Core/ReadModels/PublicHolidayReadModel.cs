using ThingsBooksy.Shared.Abstractions.EventPayloads.Calendar;

namespace ThingsBooksy.Modules.Availability.Core.ReadModels;

internal class PublicHolidayReadModel
{
    public Guid Id { get; private set; }
    public DateOnly Date { get; private set; }
    public string LocalName { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int Year { get; private set; }

    private PublicHolidayReadModel() { }

    internal static PublicHolidayReadModel Upsert(PublicHolidayDto dto)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Date = dto.Date,
            LocalName = dto.LocalName,
            Name = dto.Name,
            Year = dto.Date.Year
        };
}
