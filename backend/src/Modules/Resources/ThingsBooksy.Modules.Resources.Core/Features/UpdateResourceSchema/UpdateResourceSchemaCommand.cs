using System;
using System.Collections.Generic;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Shared.Abstractions.Commands;

namespace ThingsBooksy.Modules.Resources.Core.Features.UpdateResourceSchema;

internal record PropertyDefinitionUpdateInput(Guid? Id, string Name, PropertyDataType DataType, bool IsRequired);

internal record UpdateResourceSchemaCommand(
    Guid SchemaId,
    Guid RequesterId,
    string Name,
    string? Description,
    IEnumerable<PropertyDefinitionUpdateInput> PropertyDefinitions
) : ICommand;
