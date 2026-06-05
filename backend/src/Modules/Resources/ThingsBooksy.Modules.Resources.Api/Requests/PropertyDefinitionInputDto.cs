using ThingsBooksy.Modules.Resources.Core.Domain;

namespace ThingsBooksy.Modules.Resources.Api.Requests;

internal record PropertyDefinitionInputDto(string Name, PropertyDataType DataType, bool IsRequired);
