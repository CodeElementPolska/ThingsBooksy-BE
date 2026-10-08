using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Access;

/// <summary>
/// Story 016, AC-12 (ASM-6): the owner of group G lists instances filtered by a schema S' of another
/// group G' (of which they are neither owner nor member):
/// (a) groupId=G&amp;resourceSchemaId=S' → 200 with an empty items list and a null nextCursor;
/// (b) resourceSchemaId=S' alone → 403 resources_forbidden "Access to this group is forbidden.".
///
/// Arrange = EF seeding through the factories (G has its own schema and instance so that an ignored
/// filter would be visible), Act = HTTP as the owner of G, Assert = response + DB re-read.
/// </summary>
[Collection("IntegrationTestCollection")]
public class ForeignSchemaFilterTests : IntegrationTestBase
{
    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;
    private readonly ResourcesResourceSchemaFactory _schemas;
    private readonly ResourcesResourceInstanceFactory _instances;

    public ForeignSchemaFilterTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
        _schemas = new ResourcesResourceSchemaFactory(factory);
        _instances = new ResourcesResourceInstanceFactory(factory);
    }

    [Fact]
    [Trait("AC", "AC-12")]
    public async Task GetResourceInstances_AsOwnerWithOwnGroupAndForeignSchema_Returns200WithEmptyList()
    {
        // Arrange — owner A with group G (schema S, instance I); owner B with group G' (schema S', instance I')
        var ownerA = await _users.CreateUserAsync("a12_own_ownerA@test.com");
        var ownerB = await _users.CreateUserAsync("a12_own_ownerB@test.com");
        var groupG = await _groups.CreateGroupReadModelAsync(ownerA.UserId);
        var groupForeign = await _groups.CreateGroupReadModelAsync(ownerB.UserId);
        var schemaS = await _schemas.CreateResourceSchemaAsync(groupG.Id, ownerA.UserId, "Own Schema");
        await _instances.CreateResourceInstanceAsync(schemaS, ownerA.UserId, "Own Instance");
        var schemaForeign = await _schemas.CreateResourceSchemaAsync(groupForeign.Id, ownerB.UserId, "Foreign Schema");
        await _instances.CreateResourceInstanceAsync(schemaForeign, ownerB.UserId, "Foreign Instance");
        var client = new ResourcesTestClient(Factory, ownerA);

        // Act
        var response = await client.GetResourceInstancesAsync(resourceSchemaId: schemaForeign.Id, groupId: groupG.Id);

        // Assert — 200 { items: [], nextCursor: null }
        var page = await ResourcesResponseAssert.OkJsonAsync(response);
        Assert.Empty(ResourcesResponseAssert.Items(page));
        var nextCursor = ResourcesResponseAssert.FindProperty(page, "nextCursor");
        Assert.True(nextCursor.HasValue, $"Expected a 'nextCursor' property. JSON: {page.GetRawText()}");
        Assert.Equal(JsonValueKind.Null, nextCursor!.Value.ValueKind);

        // Assert — DB: both groups still hold their instance (the empty list is the filter, not missing data)
        Assert.Single(await client.GetResourceInstancesByGroupFromDbAsync(groupG.Id));
        Assert.Single(await client.GetResourceInstancesBySchemaFromDbAsync(schemaForeign.Id));
    }

    [Fact]
    [Trait("AC", "AC-12")]
    public async Task GetResourceInstances_AsOwnerWithForeignSchemaOnly_Returns403()
    {
        // Arrange
        var ownerA = await _users.CreateUserAsync("a12_only_ownerA@test.com");
        var ownerB = await _users.CreateUserAsync("a12_only_ownerB@test.com");
        var groupG = await _groups.CreateGroupReadModelAsync(ownerA.UserId);
        var groupForeign = await _groups.CreateGroupReadModelAsync(ownerB.UserId);
        var schemaS = await _schemas.CreateResourceSchemaAsync(groupG.Id, ownerA.UserId, "Own Schema");
        await _instances.CreateResourceInstanceAsync(schemaS, ownerA.UserId, "Own Instance");
        var schemaForeign = await _schemas.CreateResourceSchemaAsync(groupForeign.Id, ownerB.UserId, "Foreign Schema");
        var foreignInstance = await _instances.CreateResourceInstanceAsync(schemaForeign, ownerB.UserId, "Foreign Instance");
        var client = new ResourcesTestClient(Factory, ownerA);

        // Act
        var response = await client.GetResourceInstancesAsync(resourceSchemaId: schemaForeign.Id);

        // Assert — 403 and nothing of G' in the body
        await ResourcesResponseAssert.ErrorAsync(response, HttpStatusCode.Forbidden, "resources_forbidden",
            "Access to this group is forbidden.");
        await ResourcesResponseAssert.ContainsNoneOfAsync(response,
            foreignInstance.Id.ToString(), foreignInstance.Name, schemaForeign.Name);

        var stored = await client.GetResourceSchemaFromDbAsync(schemaForeign.Id);
        Assert.NotNull(stored);
        Assert.Equal(groupForeign.Id, stored.GroupId);
    }
}
