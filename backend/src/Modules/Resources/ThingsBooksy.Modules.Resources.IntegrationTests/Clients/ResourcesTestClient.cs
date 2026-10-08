using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.ReadModels;
using ThingsBooksy.Shared.Infrastructure.Postgres;
using ThingsBooksy.Shared.IntegrationTests;
using ThingsBooksy.Shared.IntegrationTests.Clients;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Clients;

public record CreateGroupResponse(Guid Id);

public record CreateResourceSchemaResponse(Guid Id);

public record PropertyDefinitionRequest(string Name, int DataType, bool IsRequired);

public record PropertyDefinitionUpdateRequest(Guid? Id, string Name, int DataType, bool IsRequired);

public record CreateResourceInstanceResponse(Guid Id);

public record PropertyValueRequest(Guid PropertyDefinitionId, string Value);

/// <summary>
/// Test client for the Resources module integration tests.
///
/// Wraps:
/// - ManagementGroups HTTP endpoints (used to trigger domain events that flow into
///   Resources event handlers)
/// - Resources HTTP endpoints (/resources/schemas, /resources/instances)
/// - the removed /resources/types addresses (story 016, AC-2: they must answer 404)
/// - Resources DB query helpers (assert side-effects on ResourcesDbContext, always IgnoreQueryFilters
///   unless the method name says otherwise)
/// </summary>
public class ResourcesTestClient
{
    private readonly HttpClient _client;
    private readonly ThingsBooksyWebAppFactory _factory;

    public ResourcesTestClient(ThingsBooksyWebAppFactory factory, AuthenticatedUser user)
    {
        _factory = factory;
        _client = user.Client;
    }

    /// <summary>
    /// A test client whose HTTP calls carry no bearer token (unauthenticated caller).
    /// </summary>
    public static ResourcesTestClient Anonymous(ThingsBooksyWebAppFactory factory)
        => new(factory, new AuthenticatedUser(factory.CreateClient(), Guid.Empty, "anonymous@test.com"));

    // -----------------------------------------------------------------------------------------
    // ManagementGroups HTTP methods — trigger domain events consumed by Resources handlers
    // -----------------------------------------------------------------------------------------

    public Task<HttpResponseMessage> CreateGroupAsync(string name, string? description = null)
        => _client.PostAsJsonAsync("/management-groups", new { Name = name, Description = description });

    public async Task<Guid> CreateGroupAndGetIdAsync(string name, string? description = null)
    {
        var response = await CreateGroupAsync(name, description);
        response.EnsureSuccessStatusCode();
        // Use case-insensitive options because the API returns lowercase "id" while the record
        // has an uppercase "Id" property. System.Text.Json defaults are case-sensitive.
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = await response.Content.ReadFromJsonAsync<CreateGroupResponse>(options);
        return result!.Id;
    }

    public Task<HttpResponseMessage> DeleteGroupAsync(Guid id)
        => _client.DeleteAsync($"/management-groups/{id}");

    public Task<HttpResponseMessage> AddMemberAsync(Guid groupId, string email)
        => _client.PostAsJsonAsync($"/management-groups/{groupId}/members", new { Email = email });

    public Task<HttpResponseMessage> RemoveMemberAsync(Guid groupId, Guid userId)
        => _client.DeleteAsync($"/management-groups/{groupId}/members/{userId}");

    // -----------------------------------------------------------------------------------------
    // Resources DB helpers — query ResourcesDbContext to assert side-effects
    // -----------------------------------------------------------------------------------------

    internal async Task<GroupReadModel?> GetGroupReadModelFromDbAsync(Guid groupId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        return await db.GroupReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == groupId);
    }

    internal async Task<List<GroupMemberReadModel>> GetGroupMemberReadModelsFromDbAsync(Guid groupId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        return await db.GroupMemberReadModels
            .IgnoreQueryFilters()
            .Where(x => x.GroupId == groupId)
            .ToListAsync();
    }

    internal async Task<GroupMemberReadModel?> GetGroupMemberReadModelFromDbAsync(Guid groupId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        return await db.GroupMemberReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.GroupId == groupId && x.UserId == userId);
    }

    // -----------------------------------------------------------------------------------------
    // Polling helpers — readable wrappers over DB helpers for WaitUntilAsync usage
    // -----------------------------------------------------------------------------------------

    internal async Task<bool> GroupReadModelExistsAsync(Guid groupId)
        => await GetGroupReadModelFromDbAsync(groupId) is not null;

    internal async Task<bool> GroupReadModelAbsentAsync(Guid groupId)
        => await GetGroupReadModelFromDbAsync(groupId) is null;

    internal async Task<bool> GroupMemberReadModelExistsAsync(Guid groupId, Guid userId)
        => await GetGroupMemberReadModelFromDbAsync(groupId, userId) is not null;

    internal async Task<bool> GroupMemberReadModelAbsentAsync(Guid groupId, Guid userId)
        => await GetGroupMemberReadModelFromDbAsync(groupId, userId) is null;

    // -----------------------------------------------------------------------------------------
    // Resources HTTP methods — POST /resources/schemas
    // -----------------------------------------------------------------------------------------

    public Task<HttpResponseMessage> CreateResourceSchemaAsync(
        Guid groupId,
        string name,
        string? description = null,
        IEnumerable<PropertyDefinitionRequest>? propertyDefinitions = null)
        => _client.PostAsJsonAsync("/resources/schemas", new
        {
            GroupId = groupId,
            Name = name,
            Description = description,
            PropertyDefinitions = (IEnumerable<PropertyDefinitionRequest>)(propertyDefinitions ?? Array.Empty<PropertyDefinitionRequest>())
        });

    public async Task<Guid> CreateResourceSchemaAndGetIdAsync(
        Guid groupId,
        string name,
        string? description = null,
        IEnumerable<PropertyDefinitionRequest>? propertyDefinitions = null)
    {
        var response = await CreateResourceSchemaAsync(groupId, name, description, propertyDefinitions);
        response.EnsureSuccessStatusCode();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = await response.Content.ReadFromJsonAsync<CreateResourceSchemaResponse>(options);
        return result!.Id;
    }

    // -----------------------------------------------------------------------------------------
    // Resources DB helpers — query ResourcesDbContext for ResourceSchema/ResourcePropertyDefinition
    // -----------------------------------------------------------------------------------------

    internal async Task<ResourceSchema?> GetResourceSchemaFromDbAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        return await db.ResourceSchemas
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    /// <summary>
    /// Unfiltered read (IgnoreQueryFilters): returns the row even when it is soft-deleted, so the
    /// caller can assert on <c>DeletedAt</c>. Same query as <see cref="GetResourceSchemaFromDbAsync"/>,
    /// named explicitly for soft-delete assertions.
    /// </summary>
    internal Task<ResourceSchema?> GetResourceSchemaFromDbIgnoringFiltersAsync(Guid id)
        => GetResourceSchemaFromDbAsync(id);

    /// <summary>
    /// Filtered read — deliberately WITHOUT IgnoreQueryFilters. Returns null for a soft-deleted row.
    /// Used only to prove that a soft-deleted schema is visible exclusively through IgnoreQueryFilters.
    /// </summary>
    internal async Task<ResourceSchema?> GetResourceSchemaFromDbRespectingQueryFiltersAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        return await db.ResourceSchemas
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    /// <summary>
    /// Unfiltered read of all ResourceSchema rows of a group with the given name (active and soft-deleted).
    /// </summary>
    internal async Task<List<ResourceSchema>> GetResourceSchemasByGroupAndNameFromDbIgnoringFiltersAsync(Guid groupId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        return await db.ResourceSchemas
            .IgnoreQueryFilters()
            .Where(x => x.GroupId == groupId && x.Name == name)
            .ToListAsync();
    }

    /// <summary>
    /// Unfiltered read of all ResourceSchema rows of a group (active and soft-deleted).
    /// </summary>
    internal async Task<List<ResourceSchema>> GetResourceSchemasByGroupFromDbAsync(Guid groupId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        return await db.ResourceSchemas
            .IgnoreQueryFilters()
            .Where(x => x.GroupId == groupId)
            .ToListAsync();
    }

    internal async Task<List<ResourcePropertyDefinition>> GetResourcePropertyDefinitionsFromDbAsync(Guid resourceSchemaId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        return await db.ResourcePropertyDefinitions
            .IgnoreQueryFilters()
            .Where(x => x.ResourceSchemaId == resourceSchemaId)
            .ToListAsync();
    }

    // -----------------------------------------------------------------------------------------
    // Resources HTTP methods — POST /resources/instances
    // -----------------------------------------------------------------------------------------

    public Task<HttpResponseMessage> CreateResourceInstanceAsync(
        Guid resourceSchemaId,
        string name,
        string? description = null,
        IEnumerable<PropertyValueRequest>? propertyValues = null)
        => _client.PostAsJsonAsync("/resources/instances", new
        {
            ResourceSchemaId = resourceSchemaId,
            Name = name,
            Description = description,
            PropertyValues = (IEnumerable<PropertyValueRequest>)(propertyValues ?? Array.Empty<PropertyValueRequest>())
        });

    public async Task<Guid> CreateResourceInstanceAndGetIdAsync(
        Guid resourceSchemaId,
        string name,
        string? description = null,
        IEnumerable<PropertyValueRequest>? propertyValues = null)
    {
        var response = await CreateResourceInstanceAsync(resourceSchemaId, name, description, propertyValues);
        response.EnsureSuccessStatusCode();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = await response.Content.ReadFromJsonAsync<CreateResourceInstanceResponse>(options);
        return result!.Id;
    }

    // -----------------------------------------------------------------------------------------
    // Resources DB helpers — query ResourcesDbContext for ResourceInstance/ResourcePropertyValue
    // -----------------------------------------------------------------------------------------

    internal async Task<ResourceInstance?> GetResourceInstanceFromDbAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        return await db.ResourceInstances
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    internal async Task<List<ResourcePropertyValue>> GetResourcePropertyValuesFromDbAsync(Guid instanceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        return await db.ResourcePropertyValues
            .IgnoreQueryFilters()
            .Where(x => x.ResourceInstanceId == instanceId)
            .ToListAsync();
    }

    // -----------------------------------------------------------------------------------------
    // Resources HTTP methods — PUT /resources/instances/{id}
    // -----------------------------------------------------------------------------------------

    public Task<HttpResponseMessage> UpdateResourceInstanceAsync(
        Guid id,
        string name,
        string? description = null,
        IEnumerable<PropertyValueRequest>? propertyValues = null)
        => _client.PutAsJsonAsync($"/resources/instances/{id}", new
        {
            Name = name,
            Description = description,
            PropertyValues = (IEnumerable<PropertyValueRequest>)(propertyValues ?? Array.Empty<PropertyValueRequest>())
        });

    // -----------------------------------------------------------------------------------------
    // Resources HTTP methods — DELETE /resources/instances/{id}
    // -----------------------------------------------------------------------------------------

    public Task<HttpResponseMessage> DeleteResourceInstanceAsync(Guid id)
        => _client.DeleteAsync($"/resources/instances/{id}");

    // -----------------------------------------------------------------------------------------
    // Resources HTTP methods — GET /resources/schemas and GET /resources/instances
    // -----------------------------------------------------------------------------------------

    public Task<HttpResponseMessage> GetResourceSchemaAsync(Guid id)
        => _client.GetAsync($"/resources/schemas/{id}");

    public Task<HttpResponseMessage> GetResourceSchemasAsync(Guid groupId)
        => _client.GetAsync($"/resources/schemas?groupId={groupId}");

    public Task<HttpResponseMessage> GetResourceInstanceAsync(Guid id)
        => _client.GetAsync($"/resources/instances/{id}");

    public Task<HttpResponseMessage> GetResourceInstancesAsync(
        Guid? resourceSchemaId = null,
        Guid? groupId = null,
        bool includeDeleted = false,
        Guid? afterId = null,
        int? take = null)
    {
        var qs = new List<string>();
        if (resourceSchemaId.HasValue) qs.Add($"resourceSchemaId={resourceSchemaId}");
        if (groupId.HasValue) qs.Add($"groupId={groupId}");
        if (includeDeleted) qs.Add("includeDeleted=true");
        if (afterId.HasValue) qs.Add($"afterId={afterId}");
        if (take.HasValue) qs.Add($"take={take}");
        var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";
        return _client.GetAsync($"/resources/instances{query}");
    }

    // -----------------------------------------------------------------------------------------
    // Resources DB helpers — query ResourcesDbContext for ResourceInstance list by schema/group
    // -----------------------------------------------------------------------------------------

    internal async Task<List<ResourceInstance>> GetResourceInstancesBySchemaFromDbAsync(Guid resourceSchemaId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        return await db.ResourceInstances
            .IgnoreQueryFilters()
            .Where(x => x.ResourceSchemaId == resourceSchemaId)
            .ToListAsync();
    }

    /// <summary>
    /// Unfiltered read of all instances of a resource schema, including soft-deleted ones.
    /// </summary>
    internal Task<List<ResourceInstance>> GetInstancesFromDbIgnoringFiltersAsync(Guid resourceSchemaId)
        => GetResourceInstancesBySchemaFromDbAsync(resourceSchemaId);

    internal async Task<List<ResourceInstance>> GetResourceInstancesByGroupFromDbAsync(Guid groupId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        return await db.ResourceInstances
            .IgnoreQueryFilters()
            .Where(x => x.GroupId == groupId)
            .ToListAsync();
    }

    // -----------------------------------------------------------------------------------------
    // Resources HTTP methods — PUT /resources/schemas/{id}
    // -----------------------------------------------------------------------------------------

    public Task<HttpResponseMessage> UpdateResourceSchemaAsync(
        Guid id,
        string name,
        string? description = null,
        IEnumerable<PropertyDefinitionUpdateRequest>? propertyDefinitions = null)
        => _client.PutAsJsonAsync($"/resources/schemas/{id}", new
        {
            Name = name,
            Description = description,
            PropertyDefinitions = (IEnumerable<PropertyDefinitionUpdateRequest>)(propertyDefinitions ?? Array.Empty<PropertyDefinitionUpdateRequest>())
        });

    // -----------------------------------------------------------------------------------------
    // Resources HTTP methods — DELETE /resources/schemas/{id}
    // -----------------------------------------------------------------------------------------

    public Task<HttpResponseMessage> DeleteResourceSchemaAsync(Guid id)
        => _client.DeleteAsync($"/resources/schemas/{id}");

    // -----------------------------------------------------------------------------------------
    // Removed addresses — the five schema operations under their pre-016 address /resources/types.
    // Story 016, AC-2: every one of them must answer 404 (no alias is kept). Same request shapes
    // as the schema operations above, so only the address differs.
    // -----------------------------------------------------------------------------------------

    private const string RemovedSchemasAddress = "/resources/types";

    public Task<HttpResponseMessage> CreateResourceSchemaAtRemovedAddressAsync(Guid groupId, string name)
        => _client.PostAsJsonAsync(RemovedSchemasAddress, new
        {
            GroupId = groupId,
            Name = name,
            Description = (string?)null,
            PropertyDefinitions = Array.Empty<PropertyDefinitionRequest>()
        });

    public Task<HttpResponseMessage> GetResourceSchemasAtRemovedAddressAsync(Guid groupId)
        => _client.GetAsync($"{RemovedSchemasAddress}?groupId={groupId}");

    public Task<HttpResponseMessage> GetResourceSchemaAtRemovedAddressAsync(Guid id)
        => _client.GetAsync($"{RemovedSchemasAddress}/{id}");

    public Task<HttpResponseMessage> UpdateResourceSchemaAtRemovedAddressAsync(Guid id, string name)
        => _client.PutAsJsonAsync($"{RemovedSchemasAddress}/{id}", new
        {
            Name = name,
            Description = (string?)null,
            PropertyDefinitions = Array.Empty<PropertyDefinitionUpdateRequest>()
        });

    public Task<HttpResponseMessage> DeleteResourceSchemaAtRemovedAddressAsync(Guid id)
        => _client.DeleteAsync($"{RemovedSchemasAddress}/{id}");

    private string GetConnectionString()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<IOptions<PostgresOptions>>().Value.ConnectionString;
    }
}
