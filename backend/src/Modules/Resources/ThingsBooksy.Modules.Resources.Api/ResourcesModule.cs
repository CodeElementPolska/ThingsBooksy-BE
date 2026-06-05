using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Resources.Api.Requests;
using ThingsBooksy.Modules.Resources.Core;
using ThingsBooksy.Modules.Resources.Core.Features.CreateResourceInstance;
using ThingsBooksy.Modules.Resources.Core.Features.CreateResourceType;
using ThingsBooksy.Modules.Resources.Core.Features.DeleteResourceInstance;
using ThingsBooksy.Modules.Resources.Core.Features.GetResourceInstance;
using ThingsBooksy.Modules.Resources.Core.Features.GetResourceInstances;
using ThingsBooksy.Modules.Resources.Core.Features.GetResourceType;
using ThingsBooksy.Modules.Resources.Core.Features.GetResourceTypes;
using ThingsBooksy.Modules.Resources.Core.Features.UpdateResourceInstance;
using ThingsBooksy.Modules.Resources.Core.Features.UpdateResourceType;
using ThingsBooksy.Modules.Resources.Core.Features.DeleteResourceType;
using ThingsBooksy.Shared.Abstractions.Dispatchers;
using ThingsBooksy.Shared.Abstractions.Modules;

namespace ThingsBooksy.Modules.Resources.Api;

internal sealed class ResourcesModule : IModule
{
    public string Name { get; } = "Resources";
    public IEnumerable<string> Policies { get; } = ["resources"];

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddResourcesCore(configuration);
    }

    public void Use(IApplicationBuilder app) { }

    public void Expose(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/resources/types", async (CreateResourceTypeRequest request, IDispatcher dispatcher, HttpContext context) =>
        {
            var callerId = GetUserId(context);
            var definitions = (request.PropertyDefinitions ?? [])
                .Select(d => new PropertyDefinitionInput(d.Name, d.DataType, d.IsRequired))
                .ToList();
            var command = new CreateResourceTypeCommand(request.GroupId, callerId, request.Name, request.Description, definitions, request.BufferMinutes);
            var createdId = await dispatcher.SendAsync<CreateResourceTypeCommand, Guid>(command);
            return Results.Created($"/resources/types/{createdId}", new { id = createdId });
        }).RequireAuthorization().WithTags("Resources").WithName("Create resource type");

        endpoints.MapPut("/resources/types/{id:guid}", async (Guid id, UpdateResourceTypeRequest request, IDispatcher dispatcher, HttpContext context) =>
        {
            var callerId = GetUserId(context);
            var definitions = (request.PropertyDefinitions ?? [])
                .Select(d => new PropertyDefinitionUpdateInput(d.Id, d.Name, d.DataType, d.IsRequired))
                .ToList();
            var command = new UpdateResourceTypeCommand(id, callerId, request.Name, request.Description, definitions);
            await dispatcher.SendAsync(command);
            return Results.NoContent();
        }).RequireAuthorization().WithTags("Resources").WithName("Update resource type");

        endpoints.MapDelete("/resources/types/{id:guid}", async (Guid id, IDispatcher dispatcher, HttpContext context) =>
        {
            var callerId = GetUserId(context);
            var command = new DeleteResourceTypeCommand(id, callerId);
            await dispatcher.SendAsync(command);
            return Results.NoContent();
        }).RequireAuthorization().WithTags("Resources").WithName("Delete resource type");

        endpoints.MapPost("/resources/instances", async (CreateResourceInstanceRequest request, IDispatcher dispatcher, HttpContext context) =>
        {
            var callerId = GetUserId(context);
            var propertyValues = (request.PropertyValues ?? [])
                .Select(pv => new PropertyValueInput(pv.PropertyDefinitionId, pv.Value))
                .ToList();
            var command = new CreateResourceInstanceCommand(request.ResourceTypeId, callerId, request.Name, request.Description, propertyValues);
            var createdId = await dispatcher.SendAsync<CreateResourceInstanceCommand, Guid>(command);
            return Results.Created($"/resources/instances/{createdId}", new { id = createdId });
        }).RequireAuthorization().WithTags("Resources").WithName("Create resource instance");

        endpoints.MapGet("/resources/types/{id:guid}", async (Guid id, IDispatcher dispatcher, HttpContext context) =>
        {
            var callerId = GetUserId(context);
            var result = await dispatcher.QueryAsync(new GetResourceTypeQuery(id, callerId));
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization().WithTags("Resources").WithName("Get resource type");

        endpoints.MapGet("/resources/types", async (Guid groupId, IDispatcher dispatcher, HttpContext context) =>
        {
            var callerId = GetUserId(context);
            var result = await dispatcher.QueryAsync(new GetResourceTypesQuery(groupId, callerId));
            return Results.Ok(result);
        }).RequireAuthorization().WithTags("Resources").WithName("Get resource types");

        endpoints.MapGet("/resources/instances/{id:guid}", async (Guid id, IDispatcher dispatcher, HttpContext context) =>
        {
            var callerId = GetUserId(context);
            var result = await dispatcher.QueryAsync(new GetResourceInstanceQuery(id, callerId));
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization().WithTags("Resources").WithName("Get resource instance");

        endpoints.MapGet("/resources/instances", async (
            Guid? resourceTypeId, Guid? groupId, bool? includeDeleted, Guid? afterId, int? take,
            IDispatcher dispatcher, HttpContext context) =>
        {
            var callerId = GetUserId(context);
            var query = new GetResourceInstancesQuery(resourceTypeId, groupId, includeDeleted ?? false, callerId, afterId, take ?? 20);
            var result = await dispatcher.QueryAsync(query);
            return Results.Ok(result);
        }).RequireAuthorization().WithTags("Resources").WithName("Get resource instances")
          .WithSummary("Returns a cursor-paginated list of resource instances. Use afterId + take for forward-only infinite scroll.")
          .Produces<GetResourceInstancesQueryResult>();

        endpoints.MapPut("/resources/instances/{id:guid}", async (Guid id, UpdateResourceInstanceRequest request, IDispatcher dispatcher, HttpContext context) =>
        {
            var callerId = GetUserId(context);
            var propertyValues = (request.PropertyValues ?? [])
                .Select(pv => new PropertyValueInput(pv.PropertyDefinitionId, pv.Value))
                .ToList();
            var command = new UpdateResourceInstanceCommand(id, request.Name, request.Description, propertyValues, callerId);
            await dispatcher.SendAsync(command);
            return Results.NoContent();
        }).RequireAuthorization().WithTags("Resources").WithName("Update resource instance");

        endpoints.MapDelete("/resources/instances/{id:guid}", async (Guid id, IDispatcher dispatcher, HttpContext context) =>
        {
            var callerId = GetUserId(context);
            var command = new DeleteResourceInstanceCommand(id, callerId);
            await dispatcher.SendAsync(command);
            return Results.NoContent();
        }).RequireAuthorization().WithTags("Resources").WithName("Delete resource instance");
    }

    private static Guid GetUserId(HttpContext context)
        => string.IsNullOrWhiteSpace(context.User.Identity?.Name)
            ? Guid.Empty
            : Guid.Parse(context.User.Identity.Name);
}
