namespace ThingsBooksy.Shared.Abstractions.Events.Resources;

public record ResourceSchemaDeletedEvent(Guid SchemaId, Guid GroupId) : IEvent;
