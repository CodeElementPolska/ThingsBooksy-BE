using ThingsBooksy.Shared.Abstractions.Events.ManagementGroups;

namespace ThingsBooksy.Modules.Availability.Core.ReadModels;

internal class GroupReadModel
{
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string TimeZoneId { get; private set; } = null!;

    private GroupReadModel() { }

    internal static GroupReadModel Upsert(GroupCreated @event)
        => new() { Id = @event.GroupId, OwnerId = @event.OwnerId, TimeZoneId = @event.TimeZoneId };
}
