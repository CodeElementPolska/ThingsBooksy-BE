using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceSchemas;

/// <summary>
/// Story 016, AC-2 (FR-002): the five schema operations under their pre-016 address
/// /resources/types no longer exist — every call answers 404, for the signed-in group owner
/// (with ids and data that would be valid at the new address) and for a caller without a token.
/// No alias is kept, so nothing is created, changed or deleted.
///
/// Arrange = EF seeding through the factories, Act = HTTP through <see cref="ResourcesTestClient"/>,
/// Assert = status code plus DB re-read (IgnoreQueryFilters).
/// </summary>
[Collection("IntegrationTestCollection")]
public class RemovedTypesAddressesTests : IntegrationTestBase
{
    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;
    private readonly ResourcesResourceSchemaFactory _schemas;

    public RemovedTypesAddressesTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
        _schemas = new ResourcesResourceSchemaFactory(factory);
    }

    private static void AssertNotFound(HttpResponseMessage response, string call)
        => Assert.True(response.StatusCode == HttpStatusCode.NotFound,
            $"{call}: expected 404 but got {(int)response.StatusCode} {response.StatusCode}.");

    // -----------------------------------------------------------------------------------------
    // Signed-in owner
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-2")]
    public async Task CreateResourceSchemaAtRemovedAddress_AsOwner_Returns404AndPersistsNothing()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("r016_post_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.CreateResourceSchemaAtRemovedAddressAsync(group.Id, "Via Old Address");

        // Assert
        AssertNotFound(response, "POST old address");
        Assert.Empty(await client.GetResourceSchemasByGroupFromDbAsync(group.Id));
    }

    [Fact]
    [Trait("AC", "AC-2")]
    public async Task GetResourceSchemasAtRemovedAddress_AsOwner_Returns404()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("r016_list_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Listed Elsewhere");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.GetResourceSchemasAtRemovedAddressAsync(group.Id);

        // Assert
        AssertNotFound(response, "GET old list address");
    }

    [Fact]
    [Trait("AC", "AC-2")]
    public async Task GetResourceSchemaAtRemovedAddress_AsOwnerForExistingSchema_Returns404()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("r016_get_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Existing");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.GetResourceSchemaAtRemovedAddressAsync(schema.Id);

        // Assert
        AssertNotFound(response, "GET old item address");
    }

    [Fact]
    [Trait("AC", "AC-2")]
    public async Task UpdateResourceSchemaAtRemovedAddress_AsOwnerForExistingSchema_Returns404AndLeavesSchemaUnchanged()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("r016_put_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Unchanged Name");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.UpdateResourceSchemaAtRemovedAddressAsync(schema.Id, "Changed Through Old Address");

        // Assert
        AssertNotFound(response, "PUT old item address");
        var stored = await client.GetResourceSchemaFromDbAsync(schema.Id);
        Assert.NotNull(stored);
        Assert.Equal("Unchanged Name", stored.Name);
    }

    [Fact]
    [Trait("AC", "AC-2")]
    public async Task DeleteResourceSchemaAtRemovedAddress_AsOwnerForExistingSchema_Returns404AndKeepsSchemaActive()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("r016_delete_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Still Active");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.DeleteResourceSchemaAtRemovedAddressAsync(schema.Id);

        // Assert
        AssertNotFound(response, "DELETE old item address");
        var stored = await client.GetResourceSchemaFromDbAsync(schema.Id);
        Assert.NotNull(stored);
        Assert.Null(stored.DeletedAt);
    }

    // -----------------------------------------------------------------------------------------
    // Caller without a token — 404 as well (no fallback route, no fallback authorization policy)
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-2")]
    public async Task CallRemovedSchemaAddresses_WithoutToken_Return404()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("r016_anon_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Anonymous Target");
        var anonymous = ResourcesTestClient.Anonymous(Factory);

        // Act
        var post = await anonymous.CreateResourceSchemaAtRemovedAddressAsync(group.Id, "Anonymous Attempt");
        var list = await anonymous.GetResourceSchemasAtRemovedAddressAsync(group.Id);
        var get = await anonymous.GetResourceSchemaAtRemovedAddressAsync(schema.Id);
        var put = await anonymous.UpdateResourceSchemaAtRemovedAddressAsync(schema.Id, "Anonymous Rename");
        var delete = await anonymous.DeleteResourceSchemaAtRemovedAddressAsync(schema.Id);

        // Assert
        AssertNotFound(post, "POST old address without token");
        AssertNotFound(list, "GET old list address without token");
        AssertNotFound(get, "GET old item address without token");
        AssertNotFound(put, "PUT old item address without token");
        AssertNotFound(delete, "DELETE old item address without token");

        var stored = await new ResourcesTestClient(Factory, owner).GetResourceSchemaFromDbAsync(schema.Id);
        Assert.NotNull(stored);
        Assert.Equal("Anonymous Target", stored.Name);
        Assert.Null(stored.DeletedAt);
    }
}
