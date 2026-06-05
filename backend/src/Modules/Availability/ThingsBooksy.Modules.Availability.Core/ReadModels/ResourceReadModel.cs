using ThingsBooksy.Shared.Abstractions.Events.Resources;

namespace ThingsBooksy.Modules.Availability.Core.ReadModels;

internal class ResourceReadModel
{
    public Guid Id { get; private set; }
    public Guid SchemaId { get; private set; }

    private ResourceReadModel() { }

    internal static ResourceReadModel Upsert(ResourceInstanceCreatedEvent @event)
        => new() { Id = @event.ResourceId, SchemaId = @event.SchemaId };
}
