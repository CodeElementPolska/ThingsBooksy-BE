using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Availability.Core.DAL;
using ThingsBooksy.Modules.Availability.Core.ReadModels;
using ThingsBooksy.Shared.Abstractions.Events;
using ThingsBooksy.Shared.Abstractions.EventPayloads.Calendar;
using ThingsBooksy.Shared.Abstractions.Events.Calendar;

namespace ThingsBooksy.Modules.Availability.Core.Events.Handlers;

internal sealed class HolidaysRefreshedHandler : IEventHandler<HolidaysRefreshedEvent>
{
    private readonly AvailabilityDbContext _dbContext;

    public HolidaysRefreshedHandler(AvailabilityDbContext dbContext)
        => _dbContext = dbContext;

    public async Task HandleAsync(HolidaysRefreshedEvent @event, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.PublicHolidayReadModels
            .Where(x => x.Year == @event.Year)
            .ToListAsync(cancellationToken);

        _dbContext.PublicHolidayReadModels.RemoveRange(existing);

        var newModels = @event.Holidays.Select(PublicHolidayReadModel.Upsert).ToList();
        await _dbContext.PublicHolidayReadModels.AddRangeAsync(newModels, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
