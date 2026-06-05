using System;
using System.Collections.Generic;
using ThingsBooksy.Shared.Abstractions.Commands;

namespace ThingsBooksy.Modules.Resources.Core.Features.CreateResourceType;

internal record CreateResourceTypeCommand(
    Guid GroupId,
    Guid CallerId,
    string Name,
    string? Description,
    IEnumerable<PropertyDefinitionInput> PropertyDefinitions,
    int BufferMinutes = 0
) : ICommand;
