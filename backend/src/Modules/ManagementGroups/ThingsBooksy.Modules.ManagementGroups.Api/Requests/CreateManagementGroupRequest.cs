namespace ThingsBooksy.Modules.ManagementGroups.Api.Requests;

internal record CreateManagementGroupRequest(string Name, string? Description, string TimeZoneId);
