using System;
using ThingsBooksy.Modules.Resources.Core.Features.CreateResourceSchema;
using ThingsBooksy.Modules.Resources.Core.Features.UpdateResourceSchema;

namespace ThingsBooksy.Modules.Resources.Core.Domain;

internal class ResourcePropertyDefinition
{
    public Guid Id { get; private set; }
    public Guid ResourceSchemaId { get; private set; }
    public string Name { get; private set; } = null!;
    public PropertyDataType DataType { get; private set; }
    public bool IsRequired { get; private set; }

    private ResourcePropertyDefinition() { }

    public static ResourcePropertyDefinition Create(PropertyDefinitionInput input, Guid resourceSchemaId)
        => new()
        {
            Id = Guid.CreateVersion7(),
            ResourceSchemaId = resourceSchemaId,
            Name = input.Name,
            DataType = input.DataType,
            IsRequired = input.IsRequired
        };

    public void Update(PropertyDefinitionUpdateInput input)
    {
        Name = input.Name;
        DataType = input.DataType;
        IsRequired = input.IsRequired;
    }
}
