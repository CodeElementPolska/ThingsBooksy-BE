using System;
using System.Collections.Generic;
using ThingsBooksy.Shared.Abstractions.Commands;

namespace ThingsBooksy.Modules.Resources.Core.Features.CreateResourceInstance;

internal record PropertyValueInput(Guid PropertyDefinitionId, string Value);

internal record CreateResourceInstanceCommand(
    Guid ResourceSchemaId,
    Guid CallerId,
    string Name,
    string? Description,
    IEnumerable<PropertyValueInput> PropertyValues
) : ICommand;
