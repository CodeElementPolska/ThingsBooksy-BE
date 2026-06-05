using System;
using ThingsBooksy.Shared.Abstractions.Events.ManagementGroups;

namespace ThingsBooksy.Modules.Resources.Core.ReadModels;

internal class GroupReadModel
{
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }

    private GroupReadModel() { }

    internal static GroupReadModel Upsert(GroupCreated @event)
        => new() { Id = @event.GroupId, OwnerId = @event.OwnerId };
    // TimeZoneId from the event is not projected into Resources' GroupReadModel
}
