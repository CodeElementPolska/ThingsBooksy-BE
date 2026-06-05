using System;
using System.Collections.Generic;
using ThingsBooksy.Modules.ManagementGroups.Core.Features.CreateManagementGroup;
using ThingsBooksy.Modules.ManagementGroups.Core.Features.UpdateManagementGroup;

namespace ThingsBooksy.Modules.ManagementGroups.Core.Domain;

internal class ManagementGroup
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid OwnerId { get; private set; }
    public string TimeZoneId { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    private ManagementGroup() { }

    public static ManagementGroup Create(CreateManagementGroupCommand command, DateTime now)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Name = command.Name,
            Description = command.Description,
            OwnerId = command.OwnerId,
            TimeZoneId = command.TimeZoneId,
            CreatedAt = now,
            UpdatedAt = now
        };

    public void Update(UpdateManagementGroupCommand command, DateTime now)
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

    public void Restore(DateTime now)
    {
        DeletedAt = null;
        UpdatedAt = now;
    }

    public ICollection<GroupMember> Members { get; private set; } = new List<GroupMember>();
}
