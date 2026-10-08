using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.Abstractions.Events.Resources;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceSchemas;

/// <summary>
/// Story 016, AC-1: as the group owner, every schema operation at /resources/schemas and every
/// instance operation that names a schema (field / filter <c>resourceSchemaId</c>) gives the same
/// result as before the rename — status, body shape, stored data, published events — apart from
/// the 409 code RESOURCE_SCHEMA_NAME_TAKEN (DEC-2).
///
/// Arrange = EF seeding through the factories, Act = HTTP through <see cref="ResourcesTestClient"/>,
/// Assert = DB re-read (IgnoreQueryFilters) compared with the response and the recorded events.
/// </summary>
[Collection("IntegrationTestCollection")]
public class ResourceSchemaContractTests : IntegrationTestBase
{
    private const string NameTakenCode = "RESOURCE_SCHEMA_NAME_TAKEN";
    private const string NameTakenMessage = "A schema with this name already exists in the group.";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record PropertyDefinitionBody(Guid Id, string Name, string DataType, bool IsRequired);
    private record SchemaBody(Guid Id, Guid GroupId, string Name, string? Description, DateTime CreatedAt, List<PropertyDefinitionBody> PropertyDefinitions);

    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;
    private readonly ResourcesResourceSchemaFactory _schemas;
    private readonly ResourcesResourcePropertyDefinitionFactory _definitions;
    private readonly ResourcesResourceInstanceFactory _instances;
    private readonly ResourcesResourcePropertyValueFactory _values;

    public ResourceSchemaContractTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
        _schemas = new ResourcesResourceSchemaFactory(factory);
        _definitions = new ResourcesResourcePropertyDefinitionFactory(factory);
        _instances = new ResourcesResourceInstanceFactory(factory);
        _values = new ResourcesResourcePropertyValueFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // POST /resources/schemas — 201, Location /resources/schemas/{id}, body { id }, stored, event
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-1")]
    public async Task CreateResourceSchema_AsOwner_Returns201WithLocationAndPersistsInDb()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("c016_create_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);
        Factory.PublishedMessages.Clear();

        // Act
        var response = await client.CreateResourceSchemaAsync(group.Id, "Projector", "Ceiling projector",
            new[] { new PropertyDefinitionRequest("Lumens", (int)PropertyDataType.Number, true) });

        // Assert — 201 with { id } and Location /resources/schemas/{id}
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 but got {(int)response.StatusCode}. Body: {raw}");
        var body = JsonSerializer.Deserialize<CreateResourceSchemaResponse>(raw, JsonOptions);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal($"/resources/schemas/{body.Id}", response.Headers.Location!.OriginalString);

        // Assert — DB row and property definition match the request
        var stored = await client.GetResourceSchemaFromDbAsync(body.Id);
        Assert.NotNull(stored);
        Assert.Equal(group.Id, stored.GroupId);
        Assert.Equal("Projector", stored.Name);
        Assert.Equal("Ceiling projector", stored.Description);
        Assert.Null(stored.DeletedAt);

        var definition = Assert.Single(await client.GetResourcePropertyDefinitionsFromDbAsync(body.Id));
        Assert.Equal("Lumens", definition.Name);
        Assert.Equal(PropertyDataType.Number, definition.DataType);
        Assert.True(definition.IsRequired);

        // Assert — the lifecycle event of story 015 is still published once
        var evt = Assert.Single(Factory.PublishedMessages.OfType<ResourceSchemaCreatedEvent>());
        Assert.Equal(stored.Id, evt.SchemaId);
        Assert.Equal(stored.GroupId, evt.GroupId);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/schemas?groupId= — 200, every active schema of the group, nothing else
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-1")]
    public async Task GetResourceSchemas_AsOwner_Returns200WithEverySchemaOfTheGroup()
    {
        // Arrange — two schemas in the group, one in another group of the same owner
        var owner = await _users.CreateUserAsync("c016_list_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var otherGroup = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Chair");
        await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Table");
        await _schemas.CreateResourceSchemaAsync(otherGroup.Id, owner.UserId, "Elsewhere");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.GetResourceSchemasAsync(group.Id);

        // Assert — 200 and exactly the group's schemas as stored in the DB
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200 but got {(int)response.StatusCode}. Body: {raw}");
        var body = JsonSerializer.Deserialize<List<SchemaBody>>(raw, JsonOptions);
        Assert.NotNull(body);

        var stored = await client.GetResourceSchemasByGroupFromDbAsync(group.Id);
        Assert.Equal(
            stored.Select(s => s.Id).OrderBy(id => id),
            body.Select(s => s.Id).OrderBy(id => id));
        Assert.All(body, s => Assert.Equal(group.Id, s.GroupId));
        Assert.Equal(new[] { "Chair", "Table" }, body.Select(s => s.Name).OrderBy(n => n));
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/schemas/{id} — 200 with the schema and its property definitions
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-1")]
    public async Task GetResourceSchema_AsOwner_Returns200WithPropertyDefinitionsFromDb()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("c016_get_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Desk", "Standing desk");
        var colour = await _definitions.CreateResourcePropertyDefinitionAsync(schema.Id, "Colour", PropertyDataType.Text, isRequired: true);
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.GetResourceSchemaAsync(schema.Id);

        // Assert
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200 but got {(int)response.StatusCode}. Body: {raw}");
        var body = JsonSerializer.Deserialize<SchemaBody>(raw, JsonOptions);
        Assert.NotNull(body);

        var stored = await client.GetResourceSchemaFromDbAsync(schema.Id);
        Assert.NotNull(stored);
        Assert.Equal(stored.Id, body.Id);
        Assert.Equal(stored.GroupId, body.GroupId);
        Assert.Equal(stored.Name, body.Name);
        Assert.Equal(stored.Description, body.Description);

        var definition = Assert.Single(body.PropertyDefinitions);
        Assert.Equal(colour.Id, definition.Id);
        Assert.Equal("Colour", definition.Name);
        Assert.Equal(nameof(PropertyDataType.Text), definition.DataType);
        Assert.True(definition.IsRequired);
    }

    // -----------------------------------------------------------------------------------------
    // PUT /resources/schemas/{id} — 204, name / description / definitions reconciled in the DB
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-1")]
    public async Task UpdateResourceSchema_AsOwner_Returns204AndUpdatesDb()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("c016_update_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Original", "Old description");
        var colour = await _definitions.CreateResourcePropertyDefinitionAsync(schema.Id, "Colour", PropertyDataType.Text);
        var client = new ResourcesTestClient(Factory, owner);

        // Act — keep the existing definition, add a new one
        var response = await client.UpdateResourceSchemaAsync(schema.Id, "Renamed", "New description", new[]
        {
            new PropertyDefinitionUpdateRequest(colour.Id, "Colour", (int)PropertyDataType.Text, false),
            new PropertyDefinitionUpdateRequest(null, "Weight", (int)PropertyDataType.Number, true),
        });

        // Assert — 204
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, $"Expected 204 but got {(int)response.StatusCode}. Body: {raw}");

        // Assert — DB
        var stored = await client.GetResourceSchemaFromDbAsync(schema.Id);
        Assert.NotNull(stored);
        Assert.Equal("Renamed", stored.Name);
        Assert.Equal("New description", stored.Description);

        var definitions = await client.GetResourcePropertyDefinitionsFromDbAsync(schema.Id);
        Assert.Equal(2, definitions.Count);
        Assert.Contains(definitions, d => d.Id == colour.Id && d.Name == "Colour");
        Assert.Contains(definitions, d => d.Name == "Weight" && d.DataType == PropertyDataType.Number && d.IsRequired);
    }

    // -----------------------------------------------------------------------------------------
    // DELETE /resources/schemas/{id} — 204, schema and its instances soft-deleted, event published
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-1")]
    public async Task DeleteResourceSchema_AsOwner_Returns204SoftDeletesAndPublishesEvent()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("c016_delete_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Kayak");
        var instance = await _instances.CreateResourceInstanceAsync(schema, owner.UserId, "Kayak 1");
        var client = new ResourcesTestClient(Factory, owner);
        Factory.PublishedMessages.Clear();

        // Act
        var response = await client.DeleteResourceSchemaAsync(schema.Id);

        // Assert — 204
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, $"Expected 204 but got {(int)response.StatusCode}. Body: {raw}");

        // Assert — DB: schema and instance soft-deleted (rows kept, DeletedAt set)
        var storedSchema = await client.GetResourceSchemaFromDbIgnoringFiltersAsync(schema.Id);
        Assert.NotNull(storedSchema);
        Assert.NotNull(storedSchema.DeletedAt);
        var storedInstance = await client.GetResourceInstanceFromDbAsync(instance.Id);
        Assert.NotNull(storedInstance);
        Assert.NotNull(storedInstance.DeletedAt);

        // Assert — exactly one ResourceSchemaDeletedEvent for the schema
        var evt = Assert.Single(Factory.PublishedMessages.OfType<ResourceSchemaDeletedEvent>());
        Assert.Equal(schema.Id, evt.SchemaId);
        Assert.Equal(group.Id, evt.GroupId);
    }

    // -----------------------------------------------------------------------------------------
    // POST /resources/instances with resourceSchemaId — 201, stored under the schema, event
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-1")]
    public async Task CreateResourceInstance_WithResourceSchemaId_Returns201AndPersistsInDb()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("c016_createinst_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Laptop");
        var ram = await _definitions.CreateResourcePropertyDefinitionAsync(schema.Id, "RAM", PropertyDataType.Number, isRequired: true);
        var client = new ResourcesTestClient(Factory, owner);
        Factory.PublishedMessages.Clear();

        // Act
        var response = await client.CreateResourceInstanceAsync(schema.Id, "Laptop 1", "Developer laptop",
            new[] { new PropertyValueRequest(ram.Id, "32") });

        // Assert — 201 with { id } and Location /resources/instances/{id}
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 but got {(int)response.StatusCode}. Body: {raw}");
        var body = JsonSerializer.Deserialize<CreateResourceInstanceResponse>(raw, JsonOptions);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal($"/resources/instances/{body.Id}", response.Headers.Location!.OriginalString);

        // Assert — DB: the instance belongs to the schema named in resourceSchemaId
        var stored = await client.GetResourceInstanceFromDbAsync(body.Id);
        Assert.NotNull(stored);
        Assert.Equal(schema.Id, stored.ResourceSchemaId);
        Assert.Equal(group.Id, stored.GroupId);
        Assert.Equal(owner.UserId, stored.OwnerId);
        Assert.Equal("Laptop 1", stored.Name);
        Assert.Equal("Developer laptop", stored.Description);

        var value = Assert.Single(await client.GetResourcePropertyValuesFromDbAsync(body.Id));
        Assert.Equal(ram.Id, value.PropertyDefinitionId);
        Assert.Equal("32", value.Value);

        // Assert — the lifecycle event carries the schema id
        var evt = Assert.Single(Factory.PublishedMessages.OfType<ResourceInstanceCreatedEvent>());
        Assert.Equal(stored.Id, evt.ResourceId);
        Assert.Equal(schema.Id, evt.SchemaId);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/instances?groupId= — every row carries resourceSchemaId
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-1")]
    public async Task GetResourceInstances_ByGroup_RowsCarryResourceSchemaId()
    {
        // Arrange — two schemas with one instance each
        var owner = await _users.CreateUserAsync("c016_listgroup_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var rooms = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Rooms");
        var cars = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Cars");
        await _instances.CreateResourceInstanceAsync(rooms, owner.UserId, "Room A");
        await _instances.CreateResourceInstanceAsync(cars, owner.UserId, "Car A");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.GetResourceInstancesAsync(groupId: group.Id);

        // Assert — rows equal the DB rows; each row carries its schema as resourceSchemaId
        var page = await ResourcesResponseAssert.OkJsonAsync(response);
        var items = ResourcesResponseAssert.Items(page);
        var stored = await client.GetResourceInstancesByGroupFromDbAsync(group.Id);
        Assert.Equal(stored.Count, items.Count);

        foreach (var item in items)
        {
            var id = ResourcesResponseAssert.GetGuid(item, "id");
            var row = Assert.Single(stored, s => s.Id == id);
            ResourcesResponseAssert.CarriesSchemaReference(item, row.ResourceSchemaId);
        }
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/instances?resourceSchemaId= — only the instances of that schema
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-1")]
    public async Task GetResourceInstances_FilteredByResourceSchemaId_Returns200WithOnlyThatSchemasInstances()
    {
        // Arrange — the filtered schema has two instances, another schema of the group has one
        var owner = await _users.CreateUserAsync("c016_filter_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var desks = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Desks");
        var lamps = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Lamps");
        await _instances.CreateResourceInstanceAsync(desks, owner.UserId, "Desk A");
        await _instances.CreateResourceInstanceAsync(desks, owner.UserId, "Desk B");
        await _instances.CreateResourceInstanceAsync(lamps, owner.UserId, "Lamp A");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.GetResourceInstancesAsync(resourceSchemaId: desks.Id);

        // Assert
        var page = await ResourcesResponseAssert.OkJsonAsync(response);
        var items = ResourcesResponseAssert.Items(page);
        var stored = await client.GetResourceInstancesBySchemaFromDbAsync(desks.Id);
        Assert.Equal(
            stored.Select(s => s.Id).OrderBy(id => id),
            items.Select(i => ResourcesResponseAssert.GetGuid(i, "id")).OrderBy(id => id));
        Assert.All(items, i => ResourcesResponseAssert.CarriesSchemaReference(i, desks.Id));
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/instances?groupId=&resourceSchemaId= — both filters applied together
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-1")]
    public async Task GetResourceInstances_FilteredByGroupAndResourceSchemaId_Returns200WithOnlyThatSchemasInstances()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("c016_filtergroup_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var boats = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Boats");
        var bikes = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Bikes");
        var boat = await _instances.CreateResourceInstanceAsync(boats, owner.UserId, "Boat A");
        await _instances.CreateResourceInstanceAsync(bikes, owner.UserId, "Bike A");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.GetResourceInstancesAsync(resourceSchemaId: boats.Id, groupId: group.Id);

        // Assert
        var page = await ResourcesResponseAssert.OkJsonAsync(response);
        var item = Assert.Single(ResourcesResponseAssert.Items(page));
        Assert.Equal(boat.Id, ResourcesResponseAssert.GetGuid(item, "id"));
        ResourcesResponseAssert.CarriesSchemaReference(item, boats.Id);

        var stored = await client.GetResourceInstanceFromDbAsync(boat.Id);
        Assert.NotNull(stored);
        Assert.Equal(boats.Id, stored.ResourceSchemaId);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/instances/{id} — the body carries resourceSchemaId
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-1")]
    public async Task GetResourceInstance_AsOwner_BodyCarriesResourceSchemaId()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("c016_getinst_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Printers");
        var colour = await _definitions.CreateResourcePropertyDefinitionAsync(schema.Id, "Colour", PropertyDataType.Boolean);
        var instance = await _instances.CreateResourceInstanceAsync(schema, owner.UserId, "Printer A", "Second floor");
        await _values.CreateResourcePropertyValueAsync(instance.Id, colour.Id, "true");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.GetResourceInstanceAsync(instance.Id);

        // Assert — body equals the DB row, the schema reference is resourceSchemaId
        var body = await ResourcesResponseAssert.OkJsonAsync(response);
        var stored = await client.GetResourceInstanceFromDbAsync(instance.Id);
        Assert.NotNull(stored);

        Assert.Equal(stored.Id, ResourcesResponseAssert.GetGuid(body, "id"));
        Assert.Equal(stored.GroupId, ResourcesResponseAssert.GetGuid(body, "groupId"));
        Assert.Equal(stored.Name, ResourcesResponseAssert.GetString(body, "name"));
        Assert.Equal(stored.Description, ResourcesResponseAssert.GetString(body, "description"));
        ResourcesResponseAssert.CarriesSchemaReference(body, stored.ResourceSchemaId);

        var values = ResourcesResponseAssert.FindProperty(body, "propertyValues");
        Assert.True(values.HasValue);
        var value = Assert.Single(values!.Value.EnumerateArray());
        Assert.Equal(colour.Id, ResourcesResponseAssert.GetGuid(value, "propertyDefinitionId"));
        Assert.Equal("true", ResourcesResponseAssert.GetString(value, "value"));
    }

    // -----------------------------------------------------------------------------------------
    // 409 — taken name: code RESOURCE_SCHEMA_NAME_TAKEN, message unchanged (DEC-2)
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-1")]
    [Trait("AC", "AC-7")]
    public async Task CreateResourceSchema_WithTakenName_Returns409WithSchemaNameTakenCode()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("c016_taken_create_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Projector");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.CreateResourceSchemaAsync(group.Id, "Projector");

        // Assert — bare { code, message } body
        await ResourcesResponseAssert.ConflictAsync(response, NameTakenCode, NameTakenMessage);

        // Assert — still exactly one schema with that name
        Assert.Single(await client.GetResourceSchemasByGroupAndNameFromDbIgnoringFiltersAsync(group.Id, "Projector"));
    }

    [Fact]
    [Trait("AC", "AC-1")]
    [Trait("AC", "AC-7")]
    public async Task UpdateResourceSchema_ToTakenName_Returns409WithSchemaNameTakenCode()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("c016_taken_update_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Screen");
        var desk = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Desk");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.UpdateResourceSchemaAsync(desk.Id, "Screen");

        // Assert
        await ResourcesResponseAssert.ConflictAsync(response, NameTakenCode, NameTakenMessage);

        var stored = await client.GetResourceSchemaFromDbAsync(desk.Id);
        Assert.NotNull(stored);
        Assert.Equal("Desk", stored.Name);
    }
}
