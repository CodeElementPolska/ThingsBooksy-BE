using System;
using System.Net;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceSchemas;

/// <summary>
/// Story 016, AC-7 (FR-005): every user-visible message of the spec's "Message list" (rows 1-6)
/// says "resource schema" and is otherwise unchanged — same status, same error code
/// (<c>resources_domain</c> / <c>resources_forbidden</c>), error envelope
/// <c>{ "errors": [ { "code", "message" } ] }</c>. Row 7 (the 409 code) is covered by
/// <see cref="ResourceSchemaContractTests"/>.
///
/// Arrange = EF seeding through the factories, Act = HTTP, Assert = response + DB re-read
/// (IgnoreQueryFilters) proving the refused call changed nothing.
/// </summary>
[Collection("IntegrationTestCollection")]
public class ResourceSchemaMessagesTests : IntegrationTestBase
{
    private const string DomainCode = "resources_domain";
    private const string ForbiddenCode = "resources_forbidden";

    private const string SchemaNotFound = "Resource schema not found.";
    private const string OnlyOwnerMayCreateSchema = "Only the group owner may create a resource schema.";
    private const string OnlyOwnerMayUpdateSchema = "Only the group owner may update a resource schema.";
    private const string OnlyOwnerMayDeleteSchema = "Only the group owner may delete a resource schema.";
    private const string SchemaNameEmpty = "Resource schema name cannot be empty.";
    private const string GroupOrSchemaRequired = "Either GroupId or ResourceSchemaId must be provided.";

    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;
    private readonly ResourcesResourceSchemaFactory _schemas;
    private readonly ResourcesResourceInstanceFactory _instances;

    public ResourceSchemaMessagesTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
        _schemas = new ResourcesResourceSchemaFactory(factory);
        _instances = new ResourcesResourceInstanceFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // Row 1 — schema id does not exist: update, delete, create instance, list by schema only
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-7")]
    public async Task UpdateResourceSchema_WithMissingId_Returns400ResourceSchemaNotFound()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("m016_update_missing@test.com");
        var client = new ResourcesTestClient(Factory, owner);
        var missingId = Guid.CreateVersion7();

        // Act
        var response = await client.UpdateResourceSchemaAsync(missingId, "Ghost");

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.BadRequest, DomainCode, SchemaNotFound);
        Assert.Null(await client.GetResourceSchemaFromDbAsync(missingId));
    }

    [Fact]
    [Trait("AC", "AC-7")]
    public async Task DeleteResourceSchema_WithMissingId_Returns400ResourceSchemaNotFound()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("m016_delete_missing@test.com");
        var client = new ResourcesTestClient(Factory, owner);
        var missingId = Guid.CreateVersion7();

        // Act
        var response = await client.DeleteResourceSchemaAsync(missingId);

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.BadRequest, DomainCode, SchemaNotFound);
        Assert.Null(await client.GetResourceSchemaFromDbAsync(missingId));
    }

    [Fact]
    [Trait("AC", "AC-7")]
    public async Task CreateResourceInstance_WithMissingSchemaId_Returns400ResourceSchemaNotFound()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("m016_createinst_missing@test.com");
        var client = new ResourcesTestClient(Factory, owner);
        var missingId = Guid.CreateVersion7();

        // Act
        var response = await client.CreateResourceInstanceAsync(missingId, "Ghost Instance");

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.BadRequest, DomainCode, SchemaNotFound);
        Assert.Empty(await client.GetResourceInstancesBySchemaFromDbAsync(missingId));
    }

    [Fact]
    [Trait("AC", "AC-7")]
    public async Task GetResourceInstances_FilteredByMissingSchemaIdOnly_Returns400ResourceSchemaNotFound()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("m016_list_missing@test.com");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.GetResourceInstancesAsync(resourceSchemaId: Guid.CreateVersion7());

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.BadRequest, DomainCode, SchemaNotFound);
    }

    // -----------------------------------------------------------------------------------------
    // Rows 2-4 — a group member who is not the owner creates / updates / deletes a schema
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-7")]
    public async Task CreateResourceSchema_AsNonOwner_Returns403OnlyOwnerMayCreateResourceSchema()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("m016_create_owner@test.com");
        var member = await _users.CreateUserAsync("m016_create_member@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _groups.AddGroupMemberAsync(group.Id, member.UserId);
        var client = new ResourcesTestClient(Factory, member);

        // Act
        var response = await client.CreateResourceSchemaAsync(group.Id, "Not Allowed");

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.Forbidden, ForbiddenCode, OnlyOwnerMayCreateSchema);
        Assert.Empty(await client.GetResourceSchemasByGroupFromDbAsync(group.Id));
    }

    [Fact]
    [Trait("AC", "AC-7")]
    public async Task UpdateResourceSchema_AsNonOwner_Returns403OnlyOwnerMayUpdateResourceSchema()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("m016_update_owner@test.com");
        var member = await _users.CreateUserAsync("m016_update_member@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _groups.AddGroupMemberAsync(group.Id, member.UserId);
        var schema = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Owner Named");
        var client = new ResourcesTestClient(Factory, member);

        // Act
        var response = await client.UpdateResourceSchemaAsync(schema.Id, "Member Renamed");

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.Forbidden, ForbiddenCode, OnlyOwnerMayUpdateSchema);
        var stored = await client.GetResourceSchemaFromDbAsync(schema.Id);
        Assert.NotNull(stored);
        Assert.Equal("Owner Named", stored.Name);
    }

    [Fact]
    [Trait("AC", "AC-7")]
    public async Task DeleteResourceSchema_AsNonOwner_Returns403OnlyOwnerMayDeleteResourceSchema()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("m016_delete_owner@test.com");
        var member = await _users.CreateUserAsync("m016_delete_member@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _groups.AddGroupMemberAsync(group.Id, member.UserId);
        var schema = await _schemas.CreateResourceSchemaAsync(group.Id, owner.UserId, "Keep Me");
        var instance = await _instances.CreateResourceInstanceAsync(schema, owner.UserId, "Keep Me Too");
        var client = new ResourcesTestClient(Factory, member);

        // Act
        var response = await client.DeleteResourceSchemaAsync(schema.Id);

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.Forbidden, ForbiddenCode, OnlyOwnerMayDeleteSchema);
        var storedSchema = await client.GetResourceSchemaFromDbAsync(schema.Id);
        Assert.NotNull(storedSchema);
        Assert.Null(storedSchema.DeletedAt);
        var storedInstance = await client.GetResourceInstanceFromDbAsync(instance.Id);
        Assert.NotNull(storedInstance);
        Assert.Null(storedInstance.DeletedAt);
    }

    // -----------------------------------------------------------------------------------------
    // Row 5 — empty schema name on create
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-7")]
    public async Task CreateResourceSchema_WithEmptyName_Returns400ResourceSchemaNameCannotBeEmpty()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("m016_emptyname_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.CreateResourceSchemaAsync(group.Id, "");

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.BadRequest, DomainCode, SchemaNameEmpty);
        Assert.Empty(await client.GetResourceSchemasByGroupFromDbAsync(group.Id));
    }

    // -----------------------------------------------------------------------------------------
    // Row 6 — instance list without groupId and without a schema filter
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-7")]
    public async Task GetResourceInstances_WithoutGroupAndSchemaFilter_Returns400GroupOrSchemaRequired()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("m016_nofilter_owner@test.com");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.GetResourceInstancesAsync();

        // Assert
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.BadRequest, DomainCode, GroupOrSchemaRequired);
    }
}
