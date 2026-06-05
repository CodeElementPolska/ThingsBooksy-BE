using ThingsBooksy.Modules.Resources.Core.Domain;

namespace ThingsBooksy.Modules.Resources.Core.Features.CreateResourceType;

internal record PropertyDefinitionInput(string Name, PropertyDataType DataType, bool IsRequired);
