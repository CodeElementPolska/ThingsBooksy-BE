using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Availability.Core.DAL;
using ThingsBooksy.Modules.Availability.Core.ReadModels;
using ThingsBooksy.Shared.Abstractions.Events;
using ThingsBooksy.Shared.Abstractions.Events.Resources;

namespace ThingsBooksy.Modules.Availability.Core.Events.Handlers;

internal sealed class ResourceInstanceCreatedHandler : IEventHandler<ResourceInstanceCreatedEvent>
{
    private readonly AvailabilityDbContext _dbContext;

    public ResourceInstanceCreatedHandler(AvailabilityDbContext dbContext)
        => _dbContext = dbContext;

    public async Task HandleAsync(ResourceInstanceCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.ResourceReadModels
            .FirstOrDefaultAsync(x => x.Id == @event.ResourceId, cancellationToken);

        var readModel = ResourceReadModel.Upsert(@event);

        if (existing is not null)
        {
            _dbContext.Entry(existing).State = EntityState.Detached;
            _dbContext.ResourceReadModels.Update(readModel);
        }
        else
        {
            await _dbContext.ResourceReadModels.AddAsync(readModel, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
