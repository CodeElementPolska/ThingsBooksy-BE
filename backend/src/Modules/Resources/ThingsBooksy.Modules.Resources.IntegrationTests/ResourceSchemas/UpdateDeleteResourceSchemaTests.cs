using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceSchemas;

/// <summary>
/// Integration tests for PUT /resources/schemas/{id} and DELETE /resources/schemas/{id}.
///
/// GroupReadModel rows are inserted directly via ResourcesGroupReadModelFactory to avoid
/// depending on async event propagation from the ManagementGroups pipeline.
/// </summary>
[Collection("IntegrationTestCollection")]
public class UpdateDeleteResourceSchemaTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record InstanceSummary(Guid Id, string Name);
    private record PagedInstancesResponse(List<InstanceSummary> Items, Guid? NextCursor);

    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;
    private readonly ResourcesResourceSchemaFactory _types;
    private readonly ResourcesResourceInstanceFactory _instances;

    public UpdateDeleteResourceSchemaTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
        _types = new ResourcesResourceSchemaFactory(factory);
        _instances = new ResourcesResourceInstanceFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // PUT /resources/schemas/{id} — happy path: name + description updated in DB
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateResourceSchema_AsOwner_Returns204AndUpdatesDb()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("updrt_happy_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var defs = new[]
        {
            new PropertyDefinitionRequest("Color", (int)PropertyDataType.Text, false),
        };
        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Original Name", "Original Desc", defs);

        // Build update payload: keep the existing definition (with its Id) and add a new one (no Id)
        var storedDefs = await client.GetResourcePropertyDefinitionsFromDbAsync(typeId);
        var existingDefId = storedDefs.First(d => d.Name == "Color").Id;

        var updateDefs = new[]
        {
            new PropertyDefinitionUpdateRequest(existingDefId, "Color", (int)PropertyDataType.Text, false),
            new PropertyDefinitionUpdateRequest(null, "Weight", (int)PropertyDataType.Number, true),
        };

        // Act
        var response = await client.UpdateResourceSchemaAsync(
            typeId, "Updated Name", "Updated Desc", updateDefs);

        // Assert — 204 No Content
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Assert — name and description updated in DB
        var resourceSchema = await client.GetResourceSchemaFromDbAsync(typeId);
        Assert.NotNull(resourceSchema);
        Assert.Equal("Updated Name", resourceSchema.Name);
        Assert.Equal("Updated Desc", resourceSchema.Description);

        // Assert — definitions reconciled: old one retained, new one added
        var definitionsAfter = await client.GetResourcePropertyDefinitionsFromDbAsync(typeId);
        Assert.Equal(2, definitionsAfter.Count);
        Assert.Contains(definitionsAfter, d => d.Name == "Color");
        Assert.Contains(definitionsAfter, d => d.Name == "Weight" && d.IsRequired);
    }

    // -----------------------------------------------------------------------------------------
    // PUT /resources/schemas/{id} — 401 Unauthenticated
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateResourceSchema_WithoutJwt_Returns401()
    {
        // Arrange — unauthenticated client
        var anonClient = Factory.CreateClient();

        // Act
        var response = await anonClient.PutAsJsonAsync($"/resources/schemas/{Guid.CreateVersion7()}", new
        {
            Name = "Attempt",
            Description = (string?)null,
            PropertyDefinitions = Array.Empty<object>()
        });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // PUT /resources/schemas/{id} — 403 Non-owner (authenticated but not the group owner)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateResourceSchema_AsNonOwner_Returns403()
    {
        // Arrange — group is owned by owner; nonOwner is a group member but not owner
        var owner = await _users.CreateUserAsync("updrt_403_owner@test.com");
        var nonOwner = await _users.CreateUserAsync("updrt_403_nonowner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _groups.AddGroupMemberAsync(group.Id, nonOwner.UserId);

        var ownerClient = new ResourcesTestClient(Factory, owner);
        var typeId = await ownerClient.CreateResourceSchemaAndGetIdAsync(group.Id, "Type For 403 Test");

        var nonOwnerClient = new ResourcesTestClient(Factory, nonOwner);

        // Act
        var response = await nonOwnerClient.UpdateResourceSchemaAsync(typeId, "Tampered Name");

        // Assert — 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Assert — name unchanged in DB
        var resourceSchema = await ownerClient.GetResourceSchemaFromDbAsync(typeId);
        Assert.NotNull(resourceSchema);
        Assert.Equal("Type For 403 Test", resourceSchema.Name);
    }

    // -----------------------------------------------------------------------------------------
    // PUT /resources/schemas/{id} — 400 Unknown resource schema ID
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateResourceSchema_WithUnknownId_Returns400()
    {
        // Arrange
        var user = await _users.CreateUserAsync("updrt_unknownid@test.com");
        var client = new ResourcesTestClient(Factory, user);

        var unknownId = Guid.CreateVersion7();

        // Act
        var response = await client.UpdateResourceSchemaAsync(unknownId, "Ghost Name");

        // Assert — resource schema not found → ResourcesDomainException (CustomException) → 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert — no type row was created as a side-effect
        var resourceSchema = await client.GetResourceSchemaFromDbAsync(unknownId);
        Assert.Null(resourceSchema);
    }

    // -----------------------------------------------------------------------------------------
    // DELETE /resources/schemas/{id} — happy path: 204 + soft-delete verified in DB (story 015, AC-8)
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "015/AC-8")]
    public async Task DeleteResourceSchema_AsOwner_Returns204AndSoftDeletesInDb()
    {
        // Arrange — a type with no instances, seeded through EF
        var owner = await _users.CreateUserAsync("delrt_happy_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var seeded = await _types.CreateResourceSchemaAsync(group.Id, owner.UserId, "Type To Delete", "Will be hidden");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.DeleteResourceSchemaAsync(seeded.Id);

        // Assert — 204 No Content
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Assert — hidden by the global query filter
        var filtered = await client.GetResourceSchemaFromDbRespectingQueryFiltersAsync(seeded.Id);
        Assert.Null(filtered);

        // Assert — soft-deleted: row still present with IgnoreQueryFilters(), DeletedAt set
        var resourceSchema = await client.GetResourceSchemaFromDbIgnoringFiltersAsync(seeded.Id);
        Assert.NotNull(resourceSchema);
        Assert.NotNull(resourceSchema.DeletedAt);
    }

    // -----------------------------------------------------------------------------------------
    // DELETE /resources/schemas/{id} — 401 Unauthenticated
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteResourceSchema_WithoutJwt_Returns401()
    {
        // Arrange — unauthenticated client
        var anonClient = Factory.CreateClient();

        // Act
        var response = await anonClient.DeleteAsync($"/resources/schemas/{Guid.CreateVersion7()}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // DELETE /resources/schemas/{id} — 403 Non-owner (authenticated but not the group owner)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteResourceSchema_AsNonOwner_Returns403()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("delrt_403_owner@test.com");
        var nonOwner = await _users.CreateUserAsync("delrt_403_nonowner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _groups.AddGroupMemberAsync(group.Id, nonOwner.UserId);

        var ownerClient = new ResourcesTestClient(Factory, owner);
        var typeId = await ownerClient.CreateResourceSchemaAndGetIdAsync(group.Id, "Type For Delete 403 Test");

        var nonOwnerClient = new ResourcesTestClient(Factory, nonOwner);

        // Act
        var response = await nonOwnerClient.DeleteResourceSchemaAsync(typeId);

        // Assert — 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Assert — type still exists in DB (not deleted)
        var resourceSchema = await ownerClient.GetResourceSchemaFromDbAsync(typeId);
        Assert.NotNull(resourceSchema);
    }

    // -----------------------------------------------------------------------------------------
    // DELETE /resources/schemas/{id} — 400 Unknown resource schema ID
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteResourceSchema_WithUnknownId_Returns400()
    {
        // Arrange
        var user = await _users.CreateUserAsync("delrt_unknownid@test.com");
        var client = new ResourcesTestClient(Factory, user);

        var unknownId = Guid.CreateVersion7();

        // Act
        var response = await client.DeleteResourceSchemaAsync(unknownId);

        // Assert — resource schema not found → ResourcesDomainException (CustomException) → 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // DELETE /resources/schemas/{id} — cascade soft-deletes instances (T074)
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "015/AC-8")]
    public async Task DeleteResourceSchema_WithInstances_SoftDeletesTypeAndCascadesToInstances()
    {
        // Arrange — a type with 3 instances, seeded through EF
        var owner = await _users.CreateUserAsync("delrt_cascade_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var seededType = await _types.CreateResourceSchemaAsync(group.Id, owner.UserId, "Type With Instances");
        var typeId = seededType.Id;
        var instanceId1 = (await _instances.CreateResourceInstanceAsync(seededType, owner.UserId, "Instance A")).Id;
        var instanceId2 = (await _instances.CreateResourceInstanceAsync(seededType, owner.UserId, "Instance B")).Id;
        var instanceId3 = (await _instances.CreateResourceInstanceAsync(seededType, owner.UserId, "Instance C")).Id;

        // Act — delete the type; cascade should soft-delete all instances
        var response = await client.DeleteResourceSchemaAsync(typeId);

        // Assert — 204 No Content
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Assert — type is soft-deleted: hidden by the query filter, present with IgnoreQueryFilters and DeletedAt set
        var filtered = await client.GetResourceSchemaFromDbRespectingQueryFiltersAsync(typeId);
        Assert.Null(filtered);

        var resourceSchema = await client.GetResourceSchemaFromDbIgnoringFiltersAsync(typeId);
        Assert.NotNull(resourceSchema);
        Assert.NotNull(resourceSchema.DeletedAt);

        // Assert — GET /resources/schemas/{id} returns 404
        var getTypeResponse = await client.GetResourceSchemaAsync(typeId);
        Assert.Equal(HttpStatusCode.NotFound, getTypeResponse.StatusCode);

        // Assert — all 3 instances are soft-deleted (DeletedAt set)
        var instance1 = await client.GetResourceInstanceFromDbAsync(instanceId1);
        var instance2 = await client.GetResourceInstanceFromDbAsync(instanceId2);
        var instance3 = await client.GetResourceInstanceFromDbAsync(instanceId3);

        Assert.NotNull(instance1);
        Assert.NotNull(instance1.DeletedAt);

        Assert.NotNull(instance2);
        Assert.NotNull(instance2.DeletedAt);

        Assert.NotNull(instance3);
        Assert.NotNull(instance3.DeletedAt);

        // Assert — instances are excluded from the default (non-deleted) list
        var listResponse = await client.GetResourceInstancesAsync(groupId: group.Id);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var pagedBody = await listResponse.Content.ReadFromJsonAsync<PagedInstancesResponse>(JsonOptions);
        Assert.NotNull(pagedBody);
        Assert.Empty(pagedBody.Items);
    }
}
