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

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceInstances;

/// <summary>
/// Integration tests for PUT /resources/instances/{id} and DELETE /resources/instances/{id} (T042–T044).
/// </summary>
[Collection("IntegrationTestCollection")]
public class UpdateDeleteResourceInstanceTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;

    private record InstanceSummary(Guid Id, string Name);
    private record PagedInstancesResponse(List<InstanceSummary> Items, Guid? NextCursor);

    public UpdateDeleteResourceInstanceTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // PUT /resources/instances/{id} — happy path: name + description updated, old values removed
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateResourceInstance_WithValidData_Returns204AndUpdatesDb()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("updri_happy@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var defs = new[] { new PropertyDefinitionRequest("Color", (int)PropertyDataType.Text, false) };
        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Chair", null, defs);
        var storedDefs = await client.GetResourcePropertyDefinitionsFromDbAsync(typeId);
        var colorDefId = storedDefs.First(d => d.Name == "Color").Id;

        var instanceId = await client.CreateResourceInstanceAndGetIdAsync(typeId, "Chair A", "Original",
            new[] { new PropertyValueRequest(colorDefId, "Red") });

        // Act
        var response = await client.UpdateResourceInstanceAsync(instanceId, "Chair B", "Updated",
            new[] { new PropertyValueRequest(colorDefId, "Blue") });

        // Assert — 204
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Assert — name + description updated in DB
        var instance = await client.GetResourceInstanceFromDbAsync(instanceId);
        Assert.NotNull(instance);
        Assert.Equal("Chair B", instance.Name);
        Assert.Equal("Updated", instance.Description);

        // Assert — old property values removed, new values stored
        var values = await client.GetResourcePropertyValuesFromDbAsync(instanceId);
        Assert.Single(values);
        Assert.Equal("Blue", values[0].Value);
        Assert.Equal(colorDefId, values[0].PropertyDefinitionId);
    }

    // -----------------------------------------------------------------------------------------
    // PUT /resources/instances/{id} — 401 Unauthenticated
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateResourceInstance_WithoutJwt_Returns401()
    {
        // Arrange — unauthenticated client
        var anonClient = Factory.CreateClient();

        // Act
        var response = await anonClient.PutAsJsonAsync($"/resources/instances/{Guid.CreateVersion7()}", new
        {
            Name = "Attempt",
            Description = (string?)null,
            PropertyValues = Array.Empty<object>()
        });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // PUT /resources/instances/{id} — 403 Non-owner
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateResourceInstance_AsNonOwner_Returns403()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("updri_403_owner@test.com");
        var nonOwner = await _users.CreateUserAsync("updri_403_nonowner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);

        var ownerClient = new ResourcesTestClient(Factory, owner);
        var typeId = await ownerClient.CreateResourceSchemaAndGetIdAsync(group.Id, "Table");
        var instanceId = await ownerClient.CreateResourceInstanceAndGetIdAsync(typeId, "Table A", "Original");

        var nonOwnerClient = new ResourcesTestClient(Factory, nonOwner);

        // Act
        var response = await nonOwnerClient.UpdateResourceInstanceAsync(instanceId, "Table B", "Tampered");

        // Assert — 403
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Assert — instance unchanged in DB
        var instance = await ownerClient.GetResourceInstanceFromDbAsync(instanceId);
        Assert.NotNull(instance);
        Assert.Equal("Table A", instance.Name);
        Assert.Equal("Original", instance.Description);
    }

    // -----------------------------------------------------------------------------------------
    // PUT /resources/instances/{id} — 400 Unknown instance id
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateResourceInstance_WithUnknownId_Returns400()
    {
        // Arrange
        var user = await _users.CreateUserAsync("updri_unknownid@test.com");
        var client = new ResourcesTestClient(Factory, user);

        var unknownId = Guid.CreateVersion7();

        // Act
        var response = await client.UpdateResourceInstanceAsync(unknownId, "Ghost");

        // Assert — instance not found → 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // PUT /resources/instances/{id} — 400 Empty name
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateResourceInstance_WithEmptyName_Returns400()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("updri_emptyname@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Stool");
        var instanceId = await client.CreateResourceInstanceAndGetIdAsync(typeId, "Stool A");

        // Act
        var response = await client.UpdateResourceInstanceAsync(instanceId, "");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert — name unchanged in DB
        var instance = await client.GetResourceInstanceFromDbAsync(instanceId);
        Assert.NotNull(instance);
        Assert.Equal("Stool A", instance.Name);
    }

    // -----------------------------------------------------------------------------------------
    // PUT /resources/instances/{id} — 400 Missing required property
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateResourceInstance_MissingRequiredProperty_Returns400()
    {
        // Arrange — type with one required property
        var owner = await _users.CreateUserAsync("updri_missingreq@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var defs = new[] { new PropertyDefinitionRequest("SerialNumber", (int)PropertyDataType.Text, true) };
        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Printer", null, defs);
        var storedDefs = await client.GetResourcePropertyDefinitionsFromDbAsync(typeId);
        var serialDef = storedDefs.First(d => d.Name == "SerialNumber");

        var instanceId = await client.CreateResourceInstanceAndGetIdAsync(typeId, "Printer One", null,
            new[] { new PropertyValueRequest(serialDef.Id, "SN-001") });

        // Act — send update without the required property
        var response = await client.UpdateResourceInstanceAsync(instanceId, "Printer One Updated");

        // Assert — missing required property → 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert — original values still in DB (not overwritten)
        var values = await client.GetResourcePropertyValuesFromDbAsync(instanceId);
        Assert.Single(values);
        Assert.Equal("SN-001", values[0].Value);
    }

    // -----------------------------------------------------------------------------------------
    // PUT /resources/instances/{id} — 400 Invalid number value
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateResourceInstance_InvalidNumberValue_Returns400()
    {
        // Arrange — type with a Number property
        var owner = await _users.CreateUserAsync("updri_invalidnum@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var defs = new[] { new PropertyDefinitionRequest("Capacity", (int)PropertyDataType.Number, false) };
        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Room", null, defs);
        var storedDefs = await client.GetResourcePropertyDefinitionsFromDbAsync(typeId);
        var capacityDef = storedDefs.First(d => d.Name == "Capacity");

        var instanceId = await client.CreateResourceInstanceAndGetIdAsync(typeId, "Room One", null,
            new[] { new PropertyValueRequest(capacityDef.Id, "10") });

        // Act — send a non-numeric string for a Number property
        var response = await client.UpdateResourceInstanceAsync(instanceId, "Room One",
            propertyValues: new[] { new PropertyValueRequest(capacityDef.Id, "not-a-number") });

        // Assert — 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert — original values still in DB (not overwritten)
        var values = await client.GetResourcePropertyValuesFromDbAsync(instanceId);
        Assert.Single(values);
        Assert.Equal("10", values[0].Value);
    }

    // -----------------------------------------------------------------------------------------
    // DELETE /resources/instances/{id} — happy path: 204 + soft-delete + excluded from GET list
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteResourceInstance_AsOwner_Returns204AndSoftDeletes()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("delri_happy@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Bike");
        var instanceId = await client.CreateResourceInstanceAndGetIdAsync(typeId, "Bike A");

        // Act
        var response = await client.DeleteResourceInstanceAsync(instanceId);

        // Assert — 204
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Assert — DeletedAt set in DB
        var instance = await client.GetResourceInstanceFromDbAsync(instanceId);
        Assert.NotNull(instance);
        Assert.NotNull(instance.DeletedAt);

        // Assert — not in default GET list
        var listResponse = await client.GetResourceInstancesAsync(resourceSchemaId: typeId);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<PagedInstancesResponse>(JsonOptions);
        Assert.NotNull(list);
        Assert.DoesNotContain(list.Items, i => i.Id == instanceId);
    }

    // -----------------------------------------------------------------------------------------
    // DELETE /resources/instances/{id} — 401 Unauthenticated
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteResourceInstance_WithoutJwt_Returns401()
    {
        // Arrange — unauthenticated client
        var anonClient = Factory.CreateClient();

        // Act
        var response = await anonClient.DeleteAsync($"/resources/instances/{Guid.CreateVersion7()}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // DELETE /resources/instances/{id} — 403 Non-owner
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteResourceInstance_AsNonOwner_Returns403()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("delri_403_owner@test.com");
        var nonOwner = await _users.CreateUserAsync("delri_403_nonowner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);

        var ownerClient = new ResourcesTestClient(Factory, owner);
        var typeId = await ownerClient.CreateResourceSchemaAndGetIdAsync(group.Id, "Lamp");
        var instanceId = await ownerClient.CreateResourceInstanceAndGetIdAsync(typeId, "Lamp A");

        var nonOwnerClient = new ResourcesTestClient(Factory, nonOwner);

        // Act
        var response = await nonOwnerClient.DeleteResourceInstanceAsync(instanceId);

        // Assert — 403
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Assert — instance not deleted in DB
        var instance = await ownerClient.GetResourceInstanceFromDbAsync(instanceId);
        Assert.NotNull(instance);
        Assert.Null(instance.DeletedAt);
    }

    // -----------------------------------------------------------------------------------------
    // DELETE /resources/instances/{id} — 400 Unknown instance id
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteResourceInstance_WithUnknownId_Returns400()
    {
        // Arrange
        var user = await _users.CreateUserAsync("delri_unknownid@test.com");
        var client = new ResourcesTestClient(Factory, user);

        var unknownId = Guid.CreateVersion7();

        // Act
        var response = await client.DeleteResourceInstanceAsync(unknownId);

        // Assert — instance not found → 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // DELETE /resources/instances/{id} — 400 Already-deleted instance (query filter hides it)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteResourceInstance_AlreadyDeleted_Returns400()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("delri_alreadydeleted@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Stand");
        var instanceId = await client.CreateResourceInstanceAndGetIdAsync(typeId, "Stand A");

        // First delete — should succeed
        var firstDelete = await client.DeleteResourceInstanceAsync(instanceId);
        Assert.Equal(HttpStatusCode.NoContent, firstDelete.StatusCode);

        // Act — second delete: query filter hides the already-deleted row → handler throws "not found" as 400
        var response = await client.DeleteResourceInstanceAsync(instanceId);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
