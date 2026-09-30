namespace ThingsBooksy.Shared.Abstractions.Events.Resources;

public record ResourceInstanceDeletedEvent(Guid ResourceId, Guid SchemaId) : IEvent;
