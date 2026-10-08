using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using ThingsBooksy.Shared.IntegrationTests.Clients;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Access;

/// <summary>
/// Story 016, AC-10: a member of group G who is not its owner gets, through /resources/schemas and
/// <c>resourceSchemaId</c>, exactly the responses of the spec's "Access baseline" (column "member",
/// rows 1-15): reads are allowed, writes are owner-only, missing ids answer as recorded. Row 14 —
/// a member listing with includeDeleted=true receives the soft-deleted instances — is intended (DEC-3).
///
/// Arrange = EF seeding through the factories, Act = HTTP as the member, Assert = response
/// (status, error code, message, body) + DB re-read (IgnoreQueryFilters); refused writes change nothing.
/// </summary>
[Collection("IntegrationTestCollection")]
public class MemberAccessBaselineTests : IntegrationTestBase
{
    private const string DomainCode = "resources_domain";
    private const string ForbiddenCode = "resources_forbidden";

    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;
    private readonly ResourcesResourceSchemaFactory _schemas;
    private readonly ResourcesResourcePropertyDefinitionFactory _definitions;
    private readonly ResourcesResourceInstanceFactory _instances;
    private readonly ResourcesResourcePropertyValueFactory _values;

    public MemberAccessBaselineTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
        _schemas = new ResourcesResourceSchemaFactory(factory);
        _definitions = new ResourcesResourcePropertyDefinitionFactory(factory);
        _instances = new ResourcesResourceInstanceFactory(factory);
        _values = new ResourcesResourcePropertyValueFactory(factory);
    }

    /// <summary>Group G (owner + one non-owner member), schema S with one property definition, instance I of S.</summary>
    private sealed record Baseline(
        AuthenticatedUser Owner,
        AuthenticatedUser Member,
        Guid GroupId,
        ResourceSchema Schema,
        ResourcePropertyDefinition Definition,
        ResourceInstance Instance,
        ResourcesTestClient MemberClient);

    private async Task<Baseline> SeedAsync(string prefix)
    {
        var owner = await _users.CreateUserAsync($"{prefix}_owner@test.com");
        var member = await _users.CreateUserAsync($"{prefix}_member@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _groups.AddGroupMemberAsync(group.Id, member.UserId);

        var schema = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Kayaks", "Sea kayaks");
        var definition = await _definitions.CreateResourcePropertyDefinitionAsync(schema.Id, "Seats", PropertyDataType.Number, isRequired: true);
        var instance = await _instances.CreateResourceInstanceAsync(schema, owner.UserId, "Kayak 1");
        await _values.CreateResourcePropertyValueAsync(instance.Id, definition.Id, "2");

        return new Baseline(owner, member, group.Id, schema, definition, instance, new ResourcesTestClient(Factory, member));
    }

    // Row 1 ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task CreateResourceSchema_AsMember_Returns403AndPersistsNothing()
    {
        // Arrange
        var b = await SeedAsync("a10_r01");

        // Act
        var response = await b.MemberClient.CreateResourceSchemaAsync(b.GroupId, "Member Schema");

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.Forbidden, ForbiddenCode,
            "Only the group owner may create a resource schema.");
        var schemas = await b.MemberClient.GetResourceSchemasByGroupFromDbAsync(b.GroupId);
        Assert.Equal(b.Schema.Id, Assert.Single(schemas).Id);
    }

    // Row 2 ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task GetResourceSchemas_AsMember_Returns200WithGroupSchemas()
    {
        // Arrange
        var b = await SeedAsync("a10_r02");

        // Act
        var response = await b.MemberClient.GetResourceSchemasAsync(b.GroupId);

        // Assert — the list equals the group's schemas in the DB
        var body = await ResourcesResponseAssert.OkJsonAsync(response);
        var listed = body.EnumerateArray().Select(s => ResourcesResponseAssert.GetGuid(s, "id")).OrderBy(id => id).ToList();
        var stored = (await b.MemberClient.GetResourceSchemasByGroupFromDbAsync(b.GroupId)).Select(s => s.Id).OrderBy(id => id).ToList();
        Assert.Equal(stored, listed);
    }

    // Rows 3 and 4 -----------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task GetResourceSchema_AsMember_ExistingReturns200AndMissingReturns404()
    {
        // Arrange
        var b = await SeedAsync("a10_r03");

        // Act
        var existing = await b.MemberClient.GetResourceSchemaAsync(b.Schema.Id);
        var missing = await b.MemberClient.GetResourceSchemaAsync(Guid.CreateVersion7());

        // Assert — row 3: the schema with its property definitions, as stored
        var body = await ResourcesResponseAssert.OkJsonAsync(existing);
        Assert.Equal(b.Schema.Id, ResourcesResponseAssert.GetGuid(body, "id"));
        Assert.Equal(b.GroupId, ResourcesResponseAssert.GetGuid(body, "groupId"));
        Assert.Equal("Kayaks", ResourcesResponseAssert.GetString(body, "name"));
        var definitions = ResourcesResponseAssert.FindProperty(body, "propertyDefinitions");
        Assert.True(definitions.HasValue);
        var definition = Assert.Single(definitions!.Value.EnumerateArray());
        Assert.Equal(b.Definition.Id, ResourcesResponseAssert.GetGuid(definition, "id"));

        var stored = await b.MemberClient.GetResourceSchemaFromDbAsync(b.Schema.Id);
        Assert.NotNull(stored);
        Assert.Equal(stored.Name, ResourcesResponseAssert.GetString(body, "name"));

        // Assert — row 4: 404 with an empty body
        await ResourcesResponseAssert.EmptyNotFoundAsync(missing);
    }

    // Row 5 ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task UpdateResourceSchema_AsMember_Returns403AndLeavesSchemaUnchanged()
    {
        // Arrange
        var b = await SeedAsync("a10_r05");

        // Act
        var response = await b.MemberClient.UpdateResourceSchemaAsync(b.Schema.Id, "Member Rename");

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.Forbidden, ForbiddenCode,
            "Only the group owner may update a resource schema.");
        var stored = await b.MemberClient.GetResourceSchemaFromDbAsync(b.Schema.Id);
        Assert.NotNull(stored);
        Assert.Equal("Kayaks", stored.Name);
        Assert.Equal("Sea kayaks", stored.Description);
    }

    // Row 6 ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task UpdateResourceSchema_AsMemberWithMissingId_Returns400ResourceSchemaNotFound()
    {
        // Arrange
        var b = await SeedAsync("a10_r06");
        var missingId = Guid.CreateVersion7();

        // Act
        var response = await b.MemberClient.UpdateResourceSchemaAsync(missingId, "Ghost");

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.BadRequest, DomainCode, "Resource schema not found.");
        Assert.Null(await b.MemberClient.GetResourceSchemaFromDbAsync(missingId));
    }

    // Row 7 ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task DeleteResourceSchema_AsMember_Returns403AndKeepsSchemaAndInstancesActive()
    {
        // Arrange
        var b = await SeedAsync("a10_r07");

        // Act
        var response = await b.MemberClient.DeleteResourceSchemaAsync(b.Schema.Id);

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.Forbidden, ForbiddenCode,
            "Only the group owner may delete a resource schema.");
        var schema = await b.MemberClient.GetResourceSchemaFromDbAsync(b.Schema.Id);
        Assert.NotNull(schema);
        Assert.Null(schema.DeletedAt);
        var instance = await b.MemberClient.GetResourceInstanceFromDbAsync(b.Instance.Id);
        Assert.NotNull(instance);
        Assert.Null(instance.DeletedAt);
    }

    // Row 8 ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task DeleteResourceSchema_AsMemberWithMissingId_Returns400ResourceSchemaNotFound()
    {
        // Arrange
        var b = await SeedAsync("a10_r08");

        // Act
        var response = await b.MemberClient.DeleteResourceSchemaAsync(Guid.CreateVersion7());

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.BadRequest, DomainCode, "Resource schema not found.");
        var schema = await b.MemberClient.GetResourceSchemaFromDbAsync(b.Schema.Id);
        Assert.NotNull(schema);
        Assert.Null(schema.DeletedAt);
    }

    // Row 9 ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task CreateResourceInstance_AsMember_Returns403AndPersistsNothing()
    {
        // Arrange
        var b = await SeedAsync("a10_r09");

        // Act
        var response = await b.MemberClient.CreateResourceInstanceAsync(b.Schema.Id, "Member Kayak",
            propertyValues: new[] { new PropertyValueRequest(b.Definition.Id, "1") });

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.Forbidden, ForbiddenCode,
            "Only the group owner may create a resource instance.");
        var instances = await b.MemberClient.GetResourceInstancesBySchemaFromDbAsync(b.Schema.Id);
        Assert.Equal(b.Instance.Id, Assert.Single(instances).Id);
    }

    // Row 10 --------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task CreateResourceInstance_AsMemberWithMissingSchemaId_Returns400ResourceSchemaNotFound()
    {
        // Arrange
        var b = await SeedAsync("a10_r10");
        var missingId = Guid.CreateVersion7();

        // Act
        var response = await b.MemberClient.CreateResourceInstanceAsync(missingId, "Ghost Kayak");

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.BadRequest, DomainCode, "Resource schema not found.");
        Assert.Empty(await b.MemberClient.GetResourceInstancesBySchemaFromDbAsync(missingId));
    }

    // Row 11 --------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task GetResourceInstances_AsMemberFilteredBySchema_Returns200RowsCarryResourceSchemaId()
    {
        // Arrange
        var b = await SeedAsync("a10_r11");

        // Act
        var response = await b.MemberClient.GetResourceInstancesAsync(resourceSchemaId: b.Schema.Id);

        // Assert
        var page = await ResourcesResponseAssert.OkJsonAsync(response);
        var item = Assert.Single(ResourcesResponseAssert.Items(page));
        Assert.Equal(b.Instance.Id, ResourcesResponseAssert.GetGuid(item, "id"));
        ResourcesResponseAssert.CarriesSchemaReference(item, b.Schema.Id);

        var stored = await b.MemberClient.GetResourceInstanceFromDbAsync(b.Instance.Id);
        Assert.NotNull(stored);
        Assert.Equal(stored.ResourceSchemaId, b.Schema.Id);
    }

    // Row 12 --------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task GetResourceInstances_AsMemberFilteredByGroupAndSchema_Returns200WithOnlyThatSchemasInstances()
    {
        // Arrange — a second schema of the same group with its own instance
        var b = await SeedAsync("a10_r12");
        var canoes = await _schemas.CreateResourceSchemaAsync(b.GroupId, b.Owner.UserId, "Canoes");
        await _instances.CreateResourceInstanceAsync(canoes, b.Owner.UserId, "Canoe 1");

        // Act
        var response = await b.MemberClient.GetResourceInstancesAsync(resourceSchemaId: b.Schema.Id, groupId: b.GroupId);

        // Assert
        var page = await ResourcesResponseAssert.OkJsonAsync(response);
        var item = Assert.Single(ResourcesResponseAssert.Items(page));
        Assert.Equal(b.Instance.Id, ResourcesResponseAssert.GetGuid(item, "id"));
        ResourcesResponseAssert.CarriesSchemaReference(item, b.Schema.Id);
        Assert.Equal(2, (await b.MemberClient.GetResourceInstancesByGroupFromDbAsync(b.GroupId)).Count);
    }

    // Row 13 --------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task GetResourceInstances_AsMemberFilteredByMissingSchema_Returns400ResourceSchemaNotFound()
    {
        // Arrange
        var b = await SeedAsync("a10_r13");

        // Act
        var response = await b.MemberClient.GetResourceInstancesAsync(resourceSchemaId: Guid.CreateVersion7());

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.BadRequest, DomainCode, "Resource schema not found.");
    }

    // Row 14 (DEC-3: intended) ----------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task GetResourceInstances_AsMemberWithIncludeDeleted_Returns200IncludingSoftDeletedInstances()
    {
        // Arrange — one active and one soft-deleted instance of S
        var b = await SeedAsync("a10_r14");
        var deleted = await _instances.CreateResourceInstanceAsync(b.Schema, b.Owner.UserId, "Kayak Retired");
        await _instances.SoftDeleteResourceInstanceAsync(deleted.Id);

        // Act
        var response = await b.MemberClient.GetResourceInstancesAsync(groupId: b.GroupId, includeDeleted: true);

        // Assert — both rows, the deleted one with deletedAt, each carrying resourceSchemaId
        var page = await ResourcesResponseAssert.OkJsonAsync(response);
        var items = ResourcesResponseAssert.Items(page);
        Assert.Equal(2, items.Count);
        Assert.All(items, i => ResourcesResponseAssert.CarriesSchemaReference(i, b.Schema.Id));

        var deletedRow = Assert.Single(items, i => ResourcesResponseAssert.GetGuid(i, "id") == deleted.Id);
        Assert.NotNull(ResourcesResponseAssert.GetString(deletedRow, "deletedAt"));

        var stored = await b.MemberClient.GetResourceInstanceFromDbAsync(deleted.Id);
        Assert.NotNull(stored);
        Assert.NotNull(stored.DeletedAt);
    }

    // Row 15 --------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task GetResourceInstance_AsMember_Returns200BodyCarriesResourceSchemaId()
    {
        // Arrange
        var b = await SeedAsync("a10_r15");

        // Act
        var response = await b.MemberClient.GetResourceInstanceAsync(b.Instance.Id);

        // Assert
        var body = await ResourcesResponseAssert.OkJsonAsync(response);
        var stored = await b.MemberClient.GetResourceInstanceFromDbAsync(b.Instance.Id);
        Assert.NotNull(stored);
        Assert.Equal(stored.Id, ResourcesResponseAssert.GetGuid(body, "id"));
        Assert.Equal(stored.Name, ResourcesResponseAssert.GetString(body, "name"));
        ResourcesResponseAssert.CarriesSchemaReference(body, stored.ResourceSchemaId);
    }
}
