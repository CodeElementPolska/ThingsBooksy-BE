using System;
using System.Collections.Generic;
using ThingsBooksy.Modules.Resources.Core.Features.CreateResourceInstance;
using ThingsBooksy.Modules.Resources.Core.Features.UpdateResourceInstance;

namespace ThingsBooksy.Modules.Resources.Core.Domain;

internal class ResourceInstance
{
    public Guid Id { get; private set; }
    public Guid ResourceSchemaId { get; private set; }
    public Guid GroupId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    private ResourceInstance() { }

    public static ResourceInstance Create(CreateResourceInstanceCommand command, Guid groupId, DateTime now)
        => new()
        {
            Id = Guid.CreateVersion7(),
            ResourceSchemaId = command.ResourceSchemaId,
            GroupId = groupId,
            Name = command.Name,
            Description = command.Description,
            OwnerId = command.CallerId,
            CreatedAt = now,
            UpdatedAt = now
        };

    public void Update(UpdateResourceInstanceCommand command, DateTime now)
    {
        Name = command.Name;
        Description = command.Description;
        UpdatedAt = now;
    }

    public void Delete(DateTime now)
    {
        DeletedAt = now;
        UpdatedAt = now;
    }

    public ICollection<ResourcePropertyValue> PropertyValues { get; private set; } = new List<ResourcePropertyValue>();
}
