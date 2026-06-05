namespace ThingsBooksy.Modules.Availability.Api.Requests;

public record ResourceRuleSetUpdateRequest(int? BufferMinutes, IReadOnlyList<NewRuleRequest> Rules);
