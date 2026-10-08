using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceSchemas;

/// <summary>
/// Integration tests for POST /resources/schemas (T030–T032).
///
/// GroupReadModel rows are inserted directly into the resources schema via
/// ResourcesGroupReadModelFactory, bypassing the ManagementGroups event pipeline.
/// This keeps each test fully self-contained and avoids timing dependencies on the
/// async event dispatcher.
/// </summary>
[Collection("IntegrationTestCollection")]
public class CreateResourceSchemaTests : IntegrationTestBase
{
    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;

    public CreateResourceSchemaTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // Happy path — 201 + DB assertion
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceSchema_WithValidData_Returns201AndPersistsInDb()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("creatert_happy_owner@test.com");
        var groupReadModel = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var definitions = new[]
        {
            new PropertyDefinitionRequest("Color", (int)PropertyDataType.Text, true),
            new PropertyDefinitionRequest("Seats", (int)PropertyDataType.Number, false),
        };

        // Act
        var response = await client.CreateResourceSchemaAsync(
            groupReadModel.Id, "Car", "Vehicle resource schema", definitions);

        // Assert — HTTP 201 with an ID
        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 but got {response.StatusCode}. Body: {rawBody}");

        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = System.Text.Json.JsonSerializer.Deserialize<CreateResourceSchemaResponse>(rawBody, options);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);

        // Assert — ResourceSchema row exists in DB
        var resourceSchema = await client.GetResourceSchemaFromDbAsync(body.Id);
        Assert.NotNull(resourceSchema);
        Assert.Equal("Car", resourceSchema.Name);
        Assert.Equal("Vehicle resource schema", resourceSchema.Description);
        Assert.Equal(groupReadModel.Id, resourceSchema.GroupId);
    }

    // -----------------------------------------------------------------------------------------
    // Property definitions are stored
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceSchema_WithPropertyDefinitions_StoresDefinitionsInDb()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("creatert_propdefs_owner@test.com");
        var groupReadModel = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var definitions = new[]
        {
            new PropertyDefinitionRequest("Color", (int)PropertyDataType.Text, true),
            new PropertyDefinitionRequest("Seats", (int)PropertyDataType.Number, false),
        };

        // Act
        var typeId = await client.CreateResourceSchemaAndGetIdAsync(
            groupReadModel.Id, "Car With Defs", null, definitions);

        // Assert — both PropertyDefinition rows exist
        var storedDefs = await client.GetResourcePropertyDefinitionsFromDbAsync(typeId);
        Assert.Equal(2, storedDefs.Count);

        var colorDef = storedDefs.Find(d => d.Name == "Color");
        Assert.NotNull(colorDef);
        Assert.Equal(PropertyDataType.Text, colorDef.DataType);
        Assert.True(colorDef.IsRequired);

        var seatsDef = storedDefs.Find(d => d.Name == "Seats");
        Assert.NotNull(seatsDef);
        Assert.Equal(PropertyDataType.Number, seatsDef.DataType);
        Assert.False(seatsDef.IsRequired);
    }

    // -----------------------------------------------------------------------------------------
    // Authorization — 401 Unauthenticated
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceSchema_WithoutJwt_Returns401()
    {
        // Arrange — unauthenticated client (no Bearer token)
        var anonClient = ResourcesTestClient.Anonymous(Factory);

        // Act
        var response = await anonClient.CreateResourceSchemaAsync(Guid.CreateVersion7(), "Should Not Create");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // Authorization — 403 Non-owner
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceSchema_AsNonOwner_Returns403()
    {
        // Arrange — group belongs to owner; non-owner tries to create a resource schema
        var owner = await _users.CreateUserAsync("creatert_403_owner@test.com");
        var nonOwner = await _users.CreateUserAsync("creatert_403_nonowner@test.com");
        var groupReadModel = await _groups.CreateGroupReadModelAsync(owner.UserId);

        var nonOwnerClient = new ResourcesTestClient(Factory, nonOwner);

        // Act
        var response = await nonOwnerClient.CreateResourceSchemaAsync(groupReadModel.Id, "Forbidden Type");

        // Assert — HTTP 403
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Assert — no ResourceSchema row was persisted
        var defs = await nonOwnerClient.GetResourcePropertyDefinitionsFromDbAsync(Guid.Empty);
        Assert.Empty(defs);
    }

    // -----------------------------------------------------------------------------------------
    // Not found — 400 Unknown groupId (no GroupReadModel row)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceSchema_WithUnknownGroupId_Returns400()
    {
        // Arrange — groupId has no matching GroupReadModel row
        var user = await _users.CreateUserAsync("creatert_404group_user@test.com");
        var client = new ResourcesTestClient(Factory, user);

        var unknownGroupId = Guid.CreateVersion7();

        // Act
        var response = await client.CreateResourceSchemaAsync(unknownGroupId, "Ghost Type");

        // Assert — GroupNotFound throws ResourcesDomainException (CustomException) → 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // Business rule — 409 Conflict when name already exists in same group
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceSchema_WithDuplicateName_Returns409()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("creatert_dupname_owner@test.com");
        var groupReadModel = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        // First creation succeeds
        var firstResponse = await client.CreateResourceSchemaAsync(groupReadModel.Id, "Car");
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act — second creation with same name in same group
        var secondResponse = await client.CreateResourceSchemaAsync(groupReadModel.Id, "Car");

        // Assert — 409 Conflict (ResourceSchemaNameAlreadyExistsException mapped to Conflict by ResourcesExceptionToResponseMapper)
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // Business rule — duplicate name scope is per-group (different group allows same name)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceSchema_SameNameInDifferentGroup_Returns201()
    {
        // Arrange — two separate groups owned by the same user
        var owner = await _users.CreateUserAsync("creatert_scopedname_owner@test.com");
        var groupReadModel1 = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var groupReadModel2 = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        // First group — create "Car"
        var firstResponse = await client.CreateResourceSchemaAsync(groupReadModel1.Id, "Car");
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act — second group with same name should succeed
        var secondResponse = await client.CreateResourceSchemaAsync(groupReadModel2.Id, "Car");

        // Assert — different group, name is not taken
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // Input validation — 400 Empty name
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceSchema_WithEmptyName_Returns400()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("creatert_emptyname_owner@test.com");
        var groupReadModel = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        // Act — send empty string as the resource schema name
        var response = await client.CreateResourceSchemaAsync(groupReadModel.Id, "");

        // Assert — empty name must be rejected before any DB write
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert — no ResourceSchema row was persisted
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ThingsBooksy.Modules.Resources.Core.DAL.ResourcesDbContext>();
        var rows = await db.ResourceSchemas
            .IgnoreQueryFilters()
            .Where(t => t.GroupId == groupReadModel.Id)
            .ToListAsync();
        Assert.Empty(rows);
    }
}
