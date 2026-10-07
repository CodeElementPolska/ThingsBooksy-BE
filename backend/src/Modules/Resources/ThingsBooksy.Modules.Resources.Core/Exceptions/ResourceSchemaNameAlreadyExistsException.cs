using System;
using ThingsBooksy.Shared.Abstractions.Exceptions;

namespace ThingsBooksy.Modules.Resources.Core.Exceptions;

internal sealed class ResourceSchemaNameAlreadyExistsException : CustomException
{
    public Guid GroupId { get; }
    public string Name { get; }

    public ResourceSchemaNameAlreadyExistsException(Guid groupId, string name)
        : base($"A schema with name '{name}' already exists in group '{groupId}'.")
    {
        GroupId = groupId;
        Name = name;
    }
}
