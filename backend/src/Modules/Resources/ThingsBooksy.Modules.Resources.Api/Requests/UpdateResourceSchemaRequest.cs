using System;
using System.Collections.Generic;
using ThingsBooksy.Modules.Resources.Core.Domain;

namespace ThingsBooksy.Modules.Resources.Api.Requests;

internal record PropertyDefinitionUpdateInputDto(Guid? Id, string Name, PropertyDataType DataType, bool IsRequired);

internal record UpdateResourceSchemaRequest(string Name, string? Description, IEnumerable<PropertyDefinitionUpdateInputDto>? PropertyDefinitions);
