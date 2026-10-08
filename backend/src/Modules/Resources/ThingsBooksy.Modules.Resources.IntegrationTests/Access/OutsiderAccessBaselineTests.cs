using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using ThingsBooksy.Shared.IntegrationTests.Clients;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Access;

/// <summary>
/// Story 016, AC-11: a signed-in user who is neither owner nor member of group G (the owner of
/// another group) gets, through /resources/schemas and <c>resourceSchemaId</c>, exactly the
/// responses of the spec's "Access baseline" (column "outsider", rows 1-15): an existing schema or
/// instance of G answers 403, a missing schema answers 404 on read and 400 on the other operations
/// (declared intended, DEC-3). No response carries data of G.
///
/// Rows whose call does not name a schema at all and therefore cannot differ before / after the
/// rename (12, 14, 15) are asserted together with row 11 in one instance-read test.
///
/// Arrange = EF seeding through the factories, Act = HTTP as the outsider, Assert = response
/// (status, error code, message, no group data) + DB re-read (IgnoreQueryFilters).
/// </summary>
[Collection("IntegrationTestCollection")]
public class OutsiderAccessBaselineTests : IntegrationTestBase
{
    private const string DomainCode = "resources_domain";
    private const string ForbiddenCode = "resources_forbidden";
    private const string GroupForbidden = "Access to this group is forbidden.";

    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;
    private readonly ResourcesResourceSchemaFactory _schemas;
    private readonly ResourcesResourcePropertyDefinitionFactory _definitions;
    private readonly ResourcesResourceInstanceFactory _instances;
    private readonly ResourcesResourcePropertyValueFactory _values;

    public OutsiderAccessBaselineTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
        _schemas = new ResourcesResourceSchemaFactory(factory);
        _definitions = new ResourcesResourcePropertyDefinitionFactory(factory);
        _instances = new ResourcesResourceInstanceFactory(factory);
        _values = new ResourcesResourcePropertyValueFactory(factory);
    }

    /// <summary>
    /// Group G of its owner with schema S (one property definition) and instance I; the outsider owns
    /// a different group and is not a member of G.
    /// </summary>
    private sealed record Baseline(
        AuthenticatedUser Owner,
        AuthenticatedUser Outsider,
        Guid GroupId,
        ResourceSchema Schema,
        ResourcePropertyDefinition Definition,
        ResourceInstance Instance,
        ResourcesTestClient OutsiderClient)
    {
        /// <summary>Values that must never appear in a response to the outsider.</summary>
        public string[] GroupData => new[]
        {
            GroupId.ToString(), Schema.Id.ToString(), Schema.Name, Instance.Id.ToString(), Instance.Name, Definition.Name,
        };
    }

    private async Task<Baseline> SeedAsync(string prefix)
    {
        var owner = await _users.CreateUserAsync($"{prefix}_owner@test.com");
        var outsider = await _users.CreateUserAsync($"{prefix}_outsider@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _groups.CreateGroupReadModelAsync(outsider.UserId);

        var schema = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Telescopes", "Observatory gear");
        var definition = await _definitions.CreateResourcePropertyDefinitionAsync(schema.Id, "Aperture", PropertyDataType.Number);
        var instance = await _instances.CreateResourceInstanceAsync(schema, owner.UserId, "Telescope Alpha");
        await _values.CreateResourcePropertyValueAsync(instance.Id, definition.Id, "200");

        return new Baseline(owner, outsider, group.Id, schema, definition, instance, new ResourcesTestClient(Factory, outsider));
    }

    private static async Task AssertRefusedWithoutGroupDataAsync(
        HttpResponseMessage response, HttpStatusCode status, string code, string message, Baseline b)
    {
        await ResourcesResponseAssert.ErrorAsync(response, status, code, message);
        await ResourcesResponseAssert.ContainsNoneOfAsync(response, b.GroupData);
    }

    // Row 1 ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-11")]
    public async Task CreateResourceSchema_AsOutsider_Returns403AndPersistsNothing()
    {
        // Arrange
        var b = await SeedAsync("a11_r01");

        // Act
        var response = await b.OutsiderClient.CreateResourceSchemaAsync(b.GroupId, "Outsider Schema");

        // Assert
        await AssertRefusedWithoutGroupDataAsync(response, HttpStatusCode.Forbidden, ForbiddenCode,
            "Only the group owner may create a resource schema.", b);
        var schemas = await b.OutsiderClient.GetResourceSchemasByGroupFromDbAsync(b.GroupId);
        Assert.Equal(b.Schema.Id, Assert.Single(schemas).Id);
    }

    // Row 2 ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-11")]
    public async Task GetResourceSchemas_AsOutsider_Returns403WithoutGroupData()
    {
        // Arrange
        var b = await SeedAsync("a11_r02");

        // Act
        var response = await b.OutsiderClient.GetResourceSchemasAsync(b.GroupId);

        // Assert
        await AssertRefusedWithoutGroupDataAsync(response, HttpStatusCode.Forbidden, ForbiddenCode, GroupForbidden, b);
    }

    // Rows 3 and 4 (DEC-3: existing 403, missing 404) -------------------------------------------------

    [Fact]
    [Trait("AC", "AC-11")]
    public async Task GetResourceSchema_AsOutsider_ExistingReturns403AndMissingReturns404()
    {
        // Arrange
        var b = await SeedAsync("a11_r03");

        // Act
        var existing = await b.OutsiderClient.GetResourceSchemaAsync(b.Schema.Id);
        var missing = await b.OutsiderClient.GetResourceSchemaAsync(Guid.CreateVersion7());

        // Assert — row 3
        await AssertRefusedWithoutGroupDataAsync(existing, HttpStatusCode.Forbidden, ForbiddenCode, GroupForbidden, b);

        // Assert — row 4
        await ResourcesResponseAssert.EmptyNotFoundAsync(missing);

        var stored = await b.OutsiderClient.GetResourceSchemaFromDbAsync(b.Schema.Id);
        Assert.NotNull(stored);
        Assert.Null(stored.DeletedAt);
    }

    // Row 5 ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-11")]
    public async Task UpdateResourceSchema_AsOutsider_Returns403AndLeavesSchemaUnchanged()
    {
        // Arrange
        var b = await SeedAsync("a11_r05");

        // Act
        var response = await b.OutsiderClient.UpdateResourceSchemaAsync(b.Schema.Id, "Outsider Rename");

        // Assert
        await AssertRefusedWithoutGroupDataAsync(response, HttpStatusCode.Forbidden, ForbiddenCode,
            "Only the group owner may update a resource schema.", b);
        var stored = await b.OutsiderClient.GetResourceSchemaFromDbAsync(b.Schema.Id);
        Assert.NotNull(stored);
        Assert.Equal("Telescopes", stored.Name);
        Assert.Equal("Observatory gear", stored.Description);
    }

    // Row 6 ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-11")]
    public async Task UpdateResourceSchema_AsOutsiderWithMissingId_Returns400ResourceSchemaNotFound()
    {
        // Arrange
        var b = await SeedAsync("a11_r06");
        var missingId = Guid.CreateVersion7();

        // Act
        var response = await b.OutsiderClient.UpdateResourceSchemaAsync(missingId, "Ghost");

        // Assert
        await AssertRefusedWithoutGroupDataAsync(response, HttpStatusCode.BadRequest, DomainCode, "Resource schema not found.", b);
        Assert.Null(await b.OutsiderClient.GetResourceSchemaFromDbAsync(missingId));
    }

    // Row 7 ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-11")]
    public async Task DeleteResourceSchema_AsOutsider_Returns403AndKeepsSchemaAndInstancesActive()
    {
        // Arrange
        var b = await SeedAsync("a11_r07");

        // Act
        var response = await b.OutsiderClient.DeleteResourceSchemaAsync(b.Schema.Id);

        // Assert
        await AssertRefusedWithoutGroupDataAsync(response, HttpStatusCode.Forbidden, ForbiddenCode,
            "Only the group owner may delete a resource schema.", b);
        var schema = await b.OutsiderClient.GetResourceSchemaFromDbAsync(b.Schema.Id);
        Assert.NotNull(schema);
        Assert.Null(schema.DeletedAt);
        var instance = await b.OutsiderClient.GetResourceInstanceFromDbAsync(b.Instance.Id);
        Assert.NotNull(instance);
        Assert.Null(instance.DeletedAt);
    }

    // Row 8 ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-11")]
    public async Task DeleteResourceSchema_AsOutsiderWithMissingId_Returns400ResourceSchemaNotFound()
    {
        // Arrange
        var b = await SeedAsync("a11_r08");

        // Act
        var response = await b.OutsiderClient.DeleteResourceSchemaAsync(Guid.CreateVersion7());

        // Assert
        await AssertRefusedWithoutGroupDataAsync(response, HttpStatusCode.BadRequest, DomainCode, "Resource schema not found.", b);
    }

    // Row 9 ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-11")]
    public async Task CreateResourceInstance_AsOutsider_Returns403AndPersistsNothing()
    {
        // Arrange
        var b = await SeedAsync("a11_r09");

        // Act
        var response = await b.OutsiderClient.CreateResourceInstanceAsync(b.Schema.Id, "Outsider Telescope",
            propertyValues: new[] { new PropertyValueRequest(b.Definition.Id, "100") });

        // Assert
        await AssertRefusedWithoutGroupDataAsync(response, HttpStatusCode.Forbidden, ForbiddenCode,
            "Only the group owner may create a resource instance.", b);
        var instances = await b.OutsiderClient.GetResourceInstancesBySchemaFromDbAsync(b.Schema.Id);
        Assert.Equal(b.Instance.Id, Assert.Single(instances).Id);
    }

    // Row 10 --------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-11")]
    public async Task CreateResourceInstance_AsOutsiderWithMissingSchemaId_Returns400ResourceSchemaNotFound()
    {
        // Arrange
        var b = await SeedAsync("a11_r10");
        var missingId = Guid.CreateVersion7();

        // Act
        var response = await b.OutsiderClient.CreateResourceInstanceAsync(missingId, "Ghost Telescope");

        // Assert
        await AssertRefusedWithoutGroupDataAsync(response, HttpStatusCode.BadRequest, DomainCode, "Resource schema not found.", b);
        Assert.Empty(await b.OutsiderClient.GetResourceInstancesBySchemaFromDbAsync(missingId));
    }

    // Rows 11, 12, 14 and 15 — every instance read of G is refused ------------------------------------

    [Fact]
    [Trait("AC", "AC-11")]
    public async Task GetResourceInstances_AsOutsider_EveryInstanceReadReturns403WithoutGroupData()
    {
        // Arrange — G also holds a soft-deleted instance (row 14)
        var b = await SeedAsync("a11_r11");
        var retired = await _instances.CreateResourceInstanceAsync(b.Schema, b.Owner.UserId, "Telescope Retired");
        await _instances.SoftDeleteResourceInstanceAsync(retired.Id);

        // Act
        var bySchema = await b.OutsiderClient.GetResourceInstancesAsync(resourceSchemaId: b.Schema.Id);                     // row 11
        var byGroupAndSchema = await b.OutsiderClient.GetResourceInstancesAsync(resourceSchemaId: b.Schema.Id, groupId: b.GroupId); // row 12
        var withDeleted = await b.OutsiderClient.GetResourceInstancesAsync(groupId: b.GroupId, includeDeleted: true);       // row 14
        var single = await b.OutsiderClient.GetResourceInstanceAsync(b.Instance.Id);                                         // row 15

        // Assert
        await AssertRefusedWithoutGroupDataAsync(bySchema, HttpStatusCode.Forbidden, ForbiddenCode, GroupForbidden, b);
        await AssertRefusedWithoutGroupDataAsync(byGroupAndSchema, HttpStatusCode.Forbidden, ForbiddenCode, GroupForbidden, b);
        await AssertRefusedWithoutGroupDataAsync(withDeleted, HttpStatusCode.Forbidden, ForbiddenCode, GroupForbidden, b);
        await ResourcesResponseAssert.ContainsNoneOfAsync(withDeleted, retired.Id.ToString(), retired.Name);
        await AssertRefusedWithoutGroupDataAsync(single, HttpStatusCode.Forbidden, ForbiddenCode, GroupForbidden, b);

        Assert.Equal(2, (await b.OutsiderClient.GetResourceInstancesByGroupFromDbAsync(b.GroupId)).Count);
    }

    // Row 13 --------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-11")]
    public async Task GetResourceInstances_AsOutsiderFilteredByMissingSchema_Returns400ResourceSchemaNotFound()
    {
        // Arrange
        var b = await SeedAsync("a11_r13");

        // Act
        var response = await b.OutsiderClient.GetResourceInstancesAsync(resourceSchemaId: Guid.CreateVersion7());

        // Assert
        await AssertRefusedWithoutGroupDataAsync(response, HttpStatusCode.BadRequest, DomainCode, "Resource schema not found.", b);
    }
}
