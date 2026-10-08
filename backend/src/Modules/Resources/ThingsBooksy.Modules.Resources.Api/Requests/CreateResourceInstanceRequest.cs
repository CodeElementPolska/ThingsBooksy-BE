using System;
using System.Collections.Generic;

namespace ThingsBooksy.Modules.Resources.Api.Requests;

internal record PropertyValueInputDto(Guid PropertyDefinitionId, string Value);

internal record CreateResourceInstanceRequest(
    Guid ResourceSchemaId,
    string Name,
    string? Description,
    IEnumerable<PropertyValueInputDto>? PropertyValues
);
