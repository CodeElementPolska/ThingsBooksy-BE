using ThingsBooksy.Shared.Abstractions.Events.Resources;

namespace ThingsBooksy.Modules.Availability.Core.ReadModels;

internal class SchemaReadModel
{
    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public int DefaultBufferMinutes { get; private set; }

    private SchemaReadModel() { }

    internal static SchemaReadModel Upsert(ResourceSchemaCreatedEvent @event)
        => new() { Id = @event.SchemaId, GroupId = @event.GroupId, DefaultBufferMinutes = @event.BufferMinutes };
}
