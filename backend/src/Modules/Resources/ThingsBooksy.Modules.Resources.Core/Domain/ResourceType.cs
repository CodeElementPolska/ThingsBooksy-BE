using System;
using System.Collections.Generic;
using ThingsBooksy.Modules.Resources.Core.Features.CreateResourceType;
using ThingsBooksy.Modules.Resources.Core.Features.UpdateResourceType;

namespace ThingsBooksy.Modules.Resources.Core.Domain;

internal class ResourceType
{
    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public int BufferMinutes { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    private ResourceType() { }

    public static ResourceType Create(CreateResourceTypeCommand command, DateTime now)
        => new()
        {
            Id = Guid.CreateVersion7(),
            GroupId = command.GroupId,
            Name = command.Name,
            Description = command.Description,
            BufferMinutes = command.BufferMinutes,
            CreatedAt = now,
            UpdatedAt = now
        };

    public void Update(UpdateResourceTypeCommand command, DateTime now)
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

    public ICollection<ResourcePropertyDefinition> PropertyDefinitions { get; private set; } = new List<ResourcePropertyDefinition>();
}
