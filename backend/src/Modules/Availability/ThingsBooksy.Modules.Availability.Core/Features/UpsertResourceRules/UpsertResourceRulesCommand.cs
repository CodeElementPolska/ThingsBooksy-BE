using ThingsBooksy.Modules.Availability.Core.Features;
using ThingsBooksy.Shared.Abstractions.Commands;

namespace ThingsBooksy.Modules.Availability.Core.Features.UpsertResourceRules;

internal record UpsertResourceRulesCommand(
    Guid ResourceId,
    Guid CallerId,
    int? BufferMinutesOverride,
    IReadOnlyList<NewAvailabilityRuleDto> Rules) : ICommand;
