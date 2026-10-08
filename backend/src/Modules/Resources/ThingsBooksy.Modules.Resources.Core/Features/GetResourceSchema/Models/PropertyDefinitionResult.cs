using System;

namespace ThingsBooksy.Modules.Resources.Core.Features.GetResourceSchema.Models;

internal record PropertyDefinitionResult(Guid Id, string Name, string DataType, bool IsRequired);
