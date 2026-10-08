using System;
using System.Collections.Generic;
using ThingsBooksy.Modules.Resources.Core.Features.GetResourceInstance.Models;

namespace ThingsBooksy.Modules.Resources.Core.Features.GetResourceInstances;

internal record ResourceInstanceRowDto(
    Guid Id,
    Guid ResourceSchemaId,
    Guid GroupId,
    string Name,
    string? Description,
    Guid OwnerId,
    DateTime CreatedAt,
    DateTime? DeletedAt,
    IReadOnlyList<PropertyValueResult> PropertyValues);
