using System;
using ThingsBooksy.Shared.Abstractions.Commands;

namespace ThingsBooksy.Modules.ManagementGroups.Core.Features.CreateManagementGroup;

internal record CreateManagementGroupCommand(string Name, string? Description, Guid OwnerId, string TimeZoneId) : ICommand;
