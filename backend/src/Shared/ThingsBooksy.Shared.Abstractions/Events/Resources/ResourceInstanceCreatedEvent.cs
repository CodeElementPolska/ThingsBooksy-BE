namespace ThingsBooksy.Shared.Abstractions.Events.Resources;

public record ResourceInstanceCreatedEvent(Guid ResourceId, Guid SchemaId) : IEvent;
