using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceSchemas;

/// <summary>
/// Additional integration tests for PUT /resources/schemas/{id} covering T046 and T047:
/// the 409 Conflict when renaming to a name already taken by another type in the group,
/// and the excludeId self-rename logic (a type may be updated with its own current name).
/// </summary>
[Collection("IntegrationTestCollection")]
public class UpdateResourceSchemaEndpointTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record ErrorBody(string Code, string Message);

    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;

    public UpdateResourceSchemaEndpointTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // T046 — 409 Conflict when renaming to a name already owned by another type in same group
    // Story 016 (DEC-2): the code is RESOURCE_SCHEMA_NAME_TAKEN, the message is unchanged.
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-1")]
    [Trait("AC", "AC-7")]
    public async Task UpdateResourceSchema_DuplicateAmongOthers_Returns409()
    {
        // Arrange — create two types in the same group
        var owner = await _users.CreateUserAsync("updrt_dupamong_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        // "Screen" is already taken
        await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Screen");
        // "Desk" is the type we will try to rename to "Screen"
        var deskId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Desk");

        // Act — attempt to rename "Desk" to "Screen" (already used by another type)
        var response = await client.UpdateResourceSchemaAsync(deskId, "Screen");

        // Assert — 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // Assert — body shape: top-level { code, message }
        var rawBody = await response.Content.ReadAsStringAsync();
        var errorBody = JsonSerializer.Deserialize<ErrorBody>(rawBody, JsonOptions);
        Assert.NotNull(errorBody);
        Assert.Equal("RESOURCE_SCHEMA_NAME_TAKEN", errorBody.Code);
        Assert.Equal("A schema with this name already exists in the group.", errorBody.Message);

        // Assert — "Desk" name is unchanged in DB (no side-effect persisted)
        var deskType = await client.GetResourceSchemaFromDbAsync(deskId);
        Assert.NotNull(deskType);
        Assert.Equal("Desk", deskType.Name);
    }

    // -----------------------------------------------------------------------------------------
    // T047 — Updating a type with its own current name returns 204 (excludeId logic)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateResourceSchema_SameNameAsItself_Succeeds()
    {
        // Arrange — create a type named "Bookshelf" and another type so uniqueness check is non-trivial
        var owner = await _users.CreateUserAsync("updrt_selfname_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var bookshelfId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Bookshelf", "Original desc");
        // A second type to confirm uniqueness check excludes only the target type's own ID
        await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Cabinet");

        // Act — update "Bookshelf" keeping the same name (only description changes)
        var response = await client.UpdateResourceSchemaAsync(bookshelfId, "Bookshelf", "Updated desc");

        // Assert — 204 No Content (excludeId prevents self-collision)
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Assert — name unchanged, description updated in DB
        var resourceSchema = await client.GetResourceSchemaFromDbAsync(bookshelfId);
        Assert.NotNull(resourceSchema);
        Assert.Equal("Bookshelf", resourceSchema.Name);
        Assert.Equal("Updated desc", resourceSchema.Description);
    }
}
