using ThingsBooksy.Modules.Availability.Core.Features;
using ThingsBooksy.Shared.Abstractions.Commands;

namespace ThingsBooksy.Modules.Availability.Core.Features.UpsertSchemaRules;

internal record UpsertSchemaRulesCommand(
    Guid SchemaId,
    Guid CallerId,
    int BufferMinutes,
    IReadOnlyList<NewAvailabilityRuleDto> Rules) : ICommand;
