using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Availability.Core.DAL;
using ThingsBooksy.Modules.Availability.Core.ReadModels;
using ThingsBooksy.Shared.Abstractions.Events;
using ThingsBooksy.Shared.Abstractions.Events.Resources;

namespace ThingsBooksy.Modules.Availability.Core.Events.Handlers;

internal sealed class ResourceSchemaCreatedHandler : IEventHandler<ResourceSchemaCreatedEvent>
{
    private readonly AvailabilityDbContext _dbContext;

    public ResourceSchemaCreatedHandler(AvailabilityDbContext dbContext)
        => _dbContext = dbContext;

    public async Task HandleAsync(ResourceSchemaCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.SchemaReadModels
            .FirstOrDefaultAsync(x => x.Id == @event.SchemaId, cancellationToken);

        var readModel = SchemaReadModel.Upsert(@event);

        if (existing is not null)
        {
            _dbContext.Entry(existing).State = EntityState.Detached;
            _dbContext.SchemaReadModels.Update(readModel);
        }
        else
        {
            await _dbContext.SchemaReadModels.AddAsync(readModel, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
