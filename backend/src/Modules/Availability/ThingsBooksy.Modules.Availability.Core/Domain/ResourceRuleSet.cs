namespace ThingsBooksy.Modules.Availability.Core.Domain;

internal class ResourceRuleSet
{
    public Guid Id { get; private set; }
    public Guid ResourceId { get; private set; }
    public Guid SchemaId { get; private set; }
    public int? BufferMinutesOverride { get; private set; }
    public ICollection<AvailabilityRule> Rules { get; private set; } = new List<AvailabilityRule>();

    private ResourceRuleSet() { }

    public static ResourceRuleSet Create(Guid resourceId, Guid schemaId)
        => new() { Id = Guid.CreateVersion7(), ResourceId = resourceId, SchemaId = schemaId };

    public void UpdateBufferOverride(int? bufferMinutesOverride) => BufferMinutesOverride = bufferMinutesOverride;
}
