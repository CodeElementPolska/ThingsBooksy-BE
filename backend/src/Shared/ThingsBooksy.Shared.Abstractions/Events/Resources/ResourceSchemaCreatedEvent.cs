namespace ThingsBooksy.Shared.Abstractions.Events.Resources;

public record ResourceSchemaCreatedEvent(Guid SchemaId, Guid GroupId) : IEvent;
