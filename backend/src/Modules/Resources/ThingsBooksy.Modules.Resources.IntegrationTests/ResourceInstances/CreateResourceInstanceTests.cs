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
/// Integration tests for POST /resources/instances (T033–T035).
///
/// GroupReadModel rows are inserted directly into the resources schema via
/// ResourcesGroupReadModelFactory, bypassing the ManagementGroups event pipeline.
/// ResourceSchema rows are created via HTTP to exercise the full creation flow as a precondition.
/// </summary>
[Collection("IntegrationTestCollection")]
public class CreateResourceInstanceTests : IntegrationTestBase
{
    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;

    public CreateResourceInstanceTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // Happy path — 201 + DB assertion (no properties)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceInstance_WithValidData_Returns201AndPersistsInDb()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("createri_happy_noprops@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Desk");

        // Act
        var response = await client.CreateResourceInstanceAsync(typeId, "Desk A", "First desk");

        // Assert — 201
        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 but got {response.StatusCode}. Body: {rawBody}");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = JsonSerializer.Deserialize<CreateResourceInstanceResponse>(rawBody, options);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);

        // Assert — DB
        var instance = await client.GetResourceInstanceFromDbAsync(body.Id);
        Assert.NotNull(instance);
        Assert.Equal("Desk A", instance.Name);
        Assert.Equal("First desk", instance.Description);
        Assert.Equal(typeId, instance.ResourceSchemaId);
        Assert.Equal(group.Id, instance.GroupId);
        Assert.Equal(owner.UserId, instance.OwnerId);

        // Assert — no property values stored
        var propertyValues = await client.GetResourcePropertyValuesFromDbAsync(body.Id);
        Assert.Empty(propertyValues);
    }

    // -----------------------------------------------------------------------------------------
    // Happy path — 201 + DB assertion (with properties)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceInstance_WithPropertyValues_Returns201AndStoresValuesInDb()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("createri_happy_withprops@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var definitions = new[]
        {
            new PropertyDefinitionRequest("Color", (int)PropertyDataType.Text, true),
            new PropertyDefinitionRequest("Seats", (int)PropertyDataType.Number, false),
        };

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Car", null, definitions);
        var storedDefs = await client.GetResourcePropertyDefinitionsFromDbAsync(typeId);

        var colorDef = storedDefs.First(d => d.Name == "Color");
        var seatsDef = storedDefs.First(d => d.Name == "Seats");

        var propertyValues = new[]
        {
            new PropertyValueRequest(colorDef.Id, "Red"),
            new PropertyValueRequest(seatsDef.Id, "5"),
        };

        // Act
        var response = await client.CreateResourceInstanceAsync(typeId, "Car One", null, propertyValues);

        // Assert — 201
        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 but got {response.StatusCode}. Body: {rawBody}");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = JsonSerializer.Deserialize<CreateResourceInstanceResponse>(rawBody, options);
        Assert.NotNull(body);

        // Assert — DB property values
        var storedValues = await client.GetResourcePropertyValuesFromDbAsync(body.Id);
        Assert.Equal(2, storedValues.Count);

        var colorValue = storedValues.FirstOrDefault(v => v.PropertyDefinitionId == colorDef.Id);
        Assert.NotNull(colorValue);
        Assert.Equal("Red", colorValue.Value);

        var seatsValue = storedValues.FirstOrDefault(v => v.PropertyDefinitionId == seatsDef.Id);
        Assert.NotNull(seatsValue);
        Assert.Equal("5", seatsValue.Value);
    }

    // -----------------------------------------------------------------------------------------
    // Authorization — 401 Unauthenticated
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceInstance_WithoutJwt_Returns401()
    {
        // Arrange — unauthenticated client
        var anonClient = Factory.CreateClient();

        // Act
        var response = await anonClient.PostAsJsonAsync("/resources/instances", new
        {
            ResourceTypeId = Guid.CreateVersion7(),
            Name = "Should Not Create",
            PropertyValues = Array.Empty<object>()
        });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // Authorization — 403 Non-owner
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceInstance_AsNonOwner_Returns403()
    {
        // Arrange — group belongs to owner; non-owner tries to create an instance
        var owner = await _users.CreateUserAsync("createri_403_owner@test.com");
        var nonOwner = await _users.CreateUserAsync("createri_403_nonowner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);

        var ownerClient = new ResourcesTestClient(Factory, owner);
        var typeId = await ownerClient.CreateResourceSchemaAndGetIdAsync(group.Id, "Chair");

        var nonOwnerClient = new ResourcesTestClient(Factory, nonOwner);

        // Act
        var response = await nonOwnerClient.CreateResourceInstanceAsync(typeId, "Chair One");

        // Assert — 403
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Assert — no instance persisted
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        var rows = await db.ResourceInstances
            .IgnoreQueryFilters()
            .Where(x => x.ResourceSchemaId == typeId)
            .ToListAsync();
        Assert.Empty(rows);
    }

    // -----------------------------------------------------------------------------------------
    // Not found — 400 Unknown ResourceSchemaId
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceInstance_WithUnknownResourceSchemaId_Returns400()
    {
        // Arrange
        var user = await _users.CreateUserAsync("createri_unknowntype@test.com");
        var client = new ResourcesTestClient(Factory, user);

        var unknownTypeId = Guid.CreateVersion7();

        // Act
        var response = await client.CreateResourceInstanceAsync(unknownTypeId, "Ghost Instance");

        // Assert — ResourceSchema not found → 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert — no instance persisted
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        var rows = await db.ResourceInstances
            .IgnoreQueryFilters()
            .Where(x => x.ResourceSchemaId == unknownTypeId)
            .ToListAsync();
        Assert.Empty(rows);
    }

    // -----------------------------------------------------------------------------------------
    // Input validation — 400 Empty name
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceInstance_WithEmptyName_Returns400()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("createri_emptyname@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Table");

        // Act
        var response = await client.CreateResourceInstanceAsync(typeId, "");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert — no instance persisted
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        var rows = await db.ResourceInstances
            .IgnoreQueryFilters()
            .Where(x => x.ResourceSchemaId == typeId)
            .ToListAsync();
        Assert.Empty(rows);
    }

    // -----------------------------------------------------------------------------------------
    // Business rule — 400 Duplicate name within same ResourceSchema
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceInstance_WithDuplicateName_Returns400()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("createri_dupname@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Monitor");

        // First creation succeeds
        var firstResponse = await client.CreateResourceInstanceAsync(typeId, "Monitor A");
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act — same name, same type
        var secondResponse = await client.CreateResourceInstanceAsync(typeId, "Monitor A");

        // Assert — 400 name uniqueness violation
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);

        // Assert — only one instance persisted
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        var rows = await db.ResourceInstances
            .IgnoreQueryFilters()
            .Where(x => x.ResourceSchemaId == typeId)
            .ToListAsync();
        Assert.Single(rows);
    }

    // -----------------------------------------------------------------------------------------
    // Business rule — name scope is per-ResourceSchema (same name in different type → 201)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceInstance_SameNameInDifferentResourceSchema_Returns201()
    {
        // Arrange — two types in the same group owned by the same user
        var owner = await _users.CreateUserAsync("createri_scopedname@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var typeId1 = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Keyboard");
        var typeId2 = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Mouse");

        // First type — create "Item A"
        var firstResponse = await client.CreateResourceInstanceAsync(typeId1, "Item A");
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act — same name in different type should succeed
        var secondResponse = await client.CreateResourceInstanceAsync(typeId2, "Item A");

        // Assert — 201
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // Business rule — 400 Missing required property
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceInstance_MissingRequiredProperty_Returns400()
    {
        // Arrange — type with one required property
        var owner = await _users.CreateUserAsync("createri_missingreq@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var definitions = new[]
        {
            new PropertyDefinitionRequest("SerialNumber", (int)PropertyDataType.Text, true),
        };

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Printer", null, definitions);

        // Act — submit no property values at all
        var response = await client.CreateResourceInstanceAsync(typeId, "Printer One");

        // Assert — missing required property → 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert — no instance persisted
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        var rows = await db.ResourceInstances
            .IgnoreQueryFilters()
            .Where(x => x.ResourceSchemaId == typeId)
            .ToListAsync();
        Assert.Empty(rows);
    }

    // -----------------------------------------------------------------------------------------
    // Business rule — 400 Invalid Number value (non-decimal string)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceInstance_WithInvalidNumberValue_Returns400()
    {
        // Arrange — type with a Number property
        var owner = await _users.CreateUserAsync("createri_invalidnum@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var definitions = new[]
        {
            new PropertyDefinitionRequest("Capacity", (int)PropertyDataType.Number, false),
        };

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Room", null, definitions);
        var storedDefs = await client.GetResourcePropertyDefinitionsFromDbAsync(typeId);
        var capacityDef = storedDefs.First(d => d.Name == "Capacity");

        // Act — send a non-numeric string for a Number property
        var propertyValues = new[] { new PropertyValueRequest(capacityDef.Id, "not-a-number") };
        var response = await client.CreateResourceInstanceAsync(typeId, "Room One", null, propertyValues);

        // Assert — 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert — no instance persisted
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        var rows = await db.ResourceInstances
            .IgnoreQueryFilters()
            .Where(x => x.ResourceSchemaId == typeId)
            .ToListAsync();
        Assert.Empty(rows);
    }

    // -----------------------------------------------------------------------------------------
    // Business rule — 400 Invalid Boolean value (non-bool string)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceInstance_WithInvalidBooleanValue_Returns400()
    {
        // Arrange — type with a Boolean property
        var owner = await _users.CreateUserAsync("createri_invalidbool@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var definitions = new[]
        {
            new PropertyDefinitionRequest("HasProjector", (int)PropertyDataType.Boolean, false),
        };

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "MeetingRoom", null, definitions);
        var storedDefs = await client.GetResourcePropertyDefinitionsFromDbAsync(typeId);
        var projectorDef = storedDefs.First(d => d.Name == "HasProjector");

        // Act — send a non-boolean string for a Boolean property
        var propertyValues = new[] { new PropertyValueRequest(projectorDef.Id, "yes-please") };
        var response = await client.CreateResourceInstanceAsync(typeId, "Room Alpha", null, propertyValues);

        // Assert — 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert — no instance persisted
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        var rows = await db.ResourceInstances
            .IgnoreQueryFilters()
            .Where(x => x.ResourceSchemaId == typeId)
            .ToListAsync();
        Assert.Empty(rows);
    }

    // -----------------------------------------------------------------------------------------
    // Business rule — 400 Unknown PropertyDefinitionId (def from a different ResourceSchema)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceInstance_WithUnknownPropertyDefinitionId_Returns400()
    {
        // Arrange — two types; use a def from type2 when creating an instance of type1
        var owner = await _users.CreateUserAsync("createri_unknownpropdef@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var type1Defs = new[] { new PropertyDefinitionRequest("Color", (int)PropertyDataType.Text, false) };
        var type2Defs = new[] { new PropertyDefinitionRequest("Weight", (int)PropertyDataType.Number, false) };

        var typeId1 = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Lamp", null, type1Defs);
        var typeId2 = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Stand", null, type2Defs);

        // Retrieve the def that belongs to type2
        var type2StoredDefs = await client.GetResourcePropertyDefinitionsFromDbAsync(typeId2);
        var weightDef = type2StoredDefs.First(d => d.Name == "Weight");

        // Act — submit a PropertyDefinitionId that belongs to type2 when creating a type1 instance
        var propertyValues = new[] { new PropertyValueRequest(weightDef.Id, "10") };
        var response = await client.CreateResourceInstanceAsync(typeId1, "Lamp One", null, propertyValues);

        // Assert — invalid property definition reference → 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert — no instance persisted
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        var rows = await db.ResourceInstances
            .IgnoreQueryFilters()
            .Where(x => x.ResourceSchemaId == typeId1)
            .ToListAsync();
        Assert.Empty(rows);
    }
}
