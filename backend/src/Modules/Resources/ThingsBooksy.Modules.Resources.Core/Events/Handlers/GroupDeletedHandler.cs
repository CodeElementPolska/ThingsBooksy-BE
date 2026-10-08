using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Shared.Abstractions.Events;
using ThingsBooksy.Shared.Abstractions.Events.ManagementGroups;

namespace ThingsBooksy.Modules.Resources.Core.Events.Handlers;

internal sealed class GroupDeletedHandler : IEventHandler<GroupDeleted>
{
    private readonly ResourcesDbContext _dbContext;

    public GroupDeletedHandler(ResourcesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(GroupDeleted @event, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Soft-delete all resource instances for the group (idempotent — zero rows matched is a no-op)
        await _dbContext.ResourceInstances
            .IgnoreQueryFilters()
            .Where(x => x.GroupId == @event.GroupId && x.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.DeletedAt, now)
                .SetProperty(x => x.UpdatedAt, now),
                cancellationToken);

        // Hard-delete all resource schemas for the group — DB cascade removes ResourcePropertyDefinitions
        await _dbContext.ResourceSchemas
            .IgnoreQueryFilters()
            .Where(x => x.GroupId == @event.GroupId)
            .ExecuteDeleteAsync(cancellationToken);

        // Remove group read-model if present (idempotent — null guard)
        var groupReadModel = await _dbContext.GroupReadModels
            .FirstOrDefaultAsync(x => x.Id == @event.GroupId, cancellationToken);

        if (groupReadModel is not null)
        {
            _dbContext.GroupReadModels.Remove(groupReadModel);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
