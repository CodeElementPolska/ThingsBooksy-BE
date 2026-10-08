using System;
using System.Collections.Generic;
using ThingsBooksy.Modules.Resources.Core.Features.GetResourceSchema.Models;

namespace ThingsBooksy.Modules.Resources.Core.Features.GetResourceSchema;

internal record GetResourceSchemaQueryResult(
    Guid Id,
    Guid GroupId,
    string Name,
    string? Description,
    DateTime CreatedAt,
    IReadOnlyList<PropertyDefinitionResult> PropertyDefinitions);
