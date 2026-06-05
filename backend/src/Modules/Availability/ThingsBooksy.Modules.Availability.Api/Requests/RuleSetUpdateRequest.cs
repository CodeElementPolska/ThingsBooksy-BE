namespace ThingsBooksy.Modules.Availability.Api.Requests;

public record RuleSetUpdateRequest(int BufferMinutes, IReadOnlyList<NewRuleRequest> Rules);
