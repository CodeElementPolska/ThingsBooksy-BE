using System;
using System.Collections.Generic;

namespace ThingsBooksy.Modules.Resources.Api.Requests;

internal record CreateResourceTypeRequest(
    Guid GroupId,
    string Name,
    string? Description,
    IEnumerable<PropertyDefinitionInputDto>? PropertyDefinitions,
    int BufferMinutes = 0
);
