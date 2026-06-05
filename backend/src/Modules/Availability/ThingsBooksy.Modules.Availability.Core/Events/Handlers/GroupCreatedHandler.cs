using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Availability.Core.DAL;
using ThingsBooksy.Modules.Availability.Core.ReadModels;
using ThingsBooksy.Shared.Abstractions.Events;
using ThingsBooksy.Shared.Abstractions.Events.ManagementGroups;

namespace ThingsBooksy.Modules.Availability.Core.Events.Handlers;

internal sealed class GroupCreatedHandler : IEventHandler<GroupCreated>
{
    private readonly AvailabilityDbContext _dbContext;

    public GroupCreatedHandler(AvailabilityDbContext dbContext)
        => _dbContext = dbContext;

    public async Task HandleAsync(GroupCreated @event, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.GroupReadModels
            .FirstOrDefaultAsync(x => x.Id == @event.GroupId, cancellationToken);

        var readModel = GroupReadModel.Upsert(@event);

        if (existing is not null)
        {
            _dbContext.Entry(existing).State = EntityState.Detached;
            _dbContext.GroupReadModels.Update(readModel);
        }
        else
        {
            await _dbContext.GroupReadModels.AddAsync(readModel, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
