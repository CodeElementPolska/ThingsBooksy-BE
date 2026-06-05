namespace ThingsBooksy.Modules.Availability.Core.Domain;

internal class SchemaRuleSet
{
    public Guid Id { get; private set; }
    public Guid SchemaId { get; private set; }
    public Guid GroupId { get; private set; }
    public int BufferMinutes { get; private set; }
    public ICollection<AvailabilityRule> Rules { get; private set; } = new List<AvailabilityRule>();

    private SchemaRuleSet() { }

    public static SchemaRuleSet Create(Guid schemaId, Guid groupId, int bufferMinutes)
        => new() { Id = Guid.CreateVersion7(), SchemaId = schemaId, GroupId = groupId, BufferMinutes = bufferMinutes };

    public void UpdateBuffer(int bufferMinutes) => BufferMinutes = bufferMinutes;
}
