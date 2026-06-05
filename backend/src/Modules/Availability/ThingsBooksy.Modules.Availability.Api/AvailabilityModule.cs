using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Availability.Api.Requests;
using ThingsBooksy.Modules.Availability.Core;
using ThingsBooksy.Modules.Availability.Core.Features;
using ThingsBooksy.Modules.Availability.Core.Features.GetResourceRules;
using ThingsBooksy.Modules.Availability.Core.Features.GetSchemaRules;
using ThingsBooksy.Modules.Availability.Core.Features.UpsertResourceRules;
using ThingsBooksy.Modules.Availability.Core.Features.UpsertSchemaRules;
using ThingsBooksy.Shared.Abstractions.Dispatchers;
using ThingsBooksy.Shared.Abstractions.Modules;

namespace ThingsBooksy.Modules.Availability.Api;

internal sealed class AvailabilityModule : IModule
{
    public string Name { get; } = "Availability";
    public IEnumerable<string> Policies { get; } = ["availability"];

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddAvailabilityCore(configuration);
    }

    public void Use(IApplicationBuilder app) { }

    public void Expose(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/availability/schemas/{schemaId:guid}/rules",
            async (Guid schemaId, IDispatcher dispatcher, HttpContext context) =>
            {
                var callerId = GetUserId(context);
                var result = await dispatcher.QueryAsync(new GetSchemaRulesQuery(schemaId, callerId));
                var response = new AvailabilityRulesResponse(
                    result.BufferMinutes,
                    result.Rules.Select(r => new AvailabilityRuleDto(r.RuleId, r.RuleType, r.RuleMode, r.DaysOfWeek, r.StartTime, r.EndTime, r.EndDayOffset, r.StartDate, r.EndDate)).ToList(),
                    null);
                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithTags("Availability")
            .WithName("Get schema availability rules")
            .Produces<AvailabilityRulesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapPut("/availability/schemas/{schemaId:guid}/rules",
            async (Guid schemaId, RuleSetUpdateRequest request, IDispatcher dispatcher, HttpContext context) =>
            {
                var callerId = GetUserId(context);
                var rules = request.Rules
                    .Select(r => new NewAvailabilityRuleDto(r.RuleType, r.RuleMode, r.DaysOfWeek, r.StartTime, r.EndTime, r.StartDate, r.EndDate))
                    .ToList();
                var command = new UpsertSchemaRulesCommand(schemaId, callerId, request.BufferMinutes, rules);
                await dispatcher.SendAsync(command);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithTags("Availability")
            .WithName("Upsert schema availability rules")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapGet("/availability/resources/{resourceId:guid}/rules",
            async (Guid resourceId, IDispatcher dispatcher, HttpContext context) =>
            {
                var callerId = GetUserId(context);
                var result = await dispatcher.QueryAsync(new GetResourceRulesQuery(resourceId, callerId));
                var inheritedRulesDtos = result.InheritedRules?.Select(r => new AvailabilityRuleDto(r.RuleId, r.RuleType, r.RuleMode, r.DaysOfWeek, r.StartTime, r.EndTime, r.EndDayOffset, r.StartDate, r.EndDate)).ToList();
                var rulesDtos = result.Rules.Select(r => new AvailabilityRuleDto(r.RuleId, r.RuleType, r.RuleMode, r.DaysOfWeek, r.StartTime, r.EndTime, r.EndDayOffset, r.StartDate, r.EndDate)).ToList();
                var response = new AvailabilityRulesResponse(result.BufferMinutes, rulesDtos, inheritedRulesDtos);
                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithTags("Availability")
            .WithName("Get resource availability rules")
            .Produces<AvailabilityRulesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapPut("/availability/resources/{resourceId:guid}/rules",
            async (Guid resourceId, ResourceRuleSetUpdateRequest request, IDispatcher dispatcher, HttpContext context) =>
            {
                var callerId = GetUserId(context);
                var rules = request.Rules
                    .Select(r => new NewAvailabilityRuleDto(r.RuleType, r.RuleMode, r.DaysOfWeek, r.StartTime, r.EndTime, r.StartDate, r.EndDate))
                    .ToList();
                var command = new UpsertResourceRulesCommand(resourceId, callerId, request.BufferMinutes, rules);
                await dispatcher.SendAsync(command);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithTags("Availability")
            .WithName("Upsert resource availability rules")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static Guid GetUserId(HttpContext context)
        => string.IsNullOrWhiteSpace(context.User.Identity?.Name)
            ? Guid.Empty
            : Guid.Parse(context.User.Identity.Name);
}
