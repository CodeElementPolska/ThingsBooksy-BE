using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceSchemas;

/// <summary>
/// Additional integration tests for POST /resources/schemas covering T044 and T045:
/// the 409 Conflict response shape when a duplicate name is used within the same group,
/// and the cross-group name-scoping rule.
/// </summary>
[Collection("IntegrationTestCollection")]
public class CreateResourceSchemaEndpointTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record ErrorBody(string Code, string Message);
    private record CreateRtResponse(Guid Id);

    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;

    public CreateResourceSchemaEndpointTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // T044 — 409 Conflict + body shape when name already exists in the same group
    // Story 016 (DEC-2): the code is RESOURCE_SCHEMA_NAME_TAKEN, the message is unchanged.
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-1")]
    [Trait("AC", "AC-7")]
    public async Task CreateResourceSchema_DuplicateNameInGroup_Returns409()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("creatert_dupname_409_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        // First creation must succeed
        var firstResponse = await client.CreateResourceSchemaAsync(group.Id, "Projector");
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act — second creation with the exact same name in the same group
        var secondResponse = await client.CreateResourceSchemaAsync(group.Id, "Projector");

        // Assert — 409 Conflict (not 400)
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        // Assert — body shape is top-level { code, message } with no wrapper
        var rawBody = await secondResponse.Content.ReadAsStringAsync();
        var errorBody = JsonSerializer.Deserialize<ErrorBody>(rawBody, JsonOptions);
        Assert.NotNull(errorBody);
        Assert.Equal("RESOURCE_SCHEMA_NAME_TAKEN", errorBody.Code);
        Assert.Equal("A schema with this name already exists in the group.", errorBody.Message);

        // Assert — only the first schema was persisted in DB
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        var rows = await db.ResourceSchemas
            .IgnoreQueryFilters()
            .Where(t => t.GroupId == group.Id && t.Name == "Projector")
            .ToListAsync();
        Assert.Single(rows);
    }

    // -----------------------------------------------------------------------------------------
    // T045 — Same name allowed in a different group (name scope is per-group)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceSchema_SameNameDifferentGroup_Succeeds()
    {
        // Arrange — two groups owned by the same user
        var owner = await _users.CreateUserAsync("creatert_samenamegroup_owner@test.com");
        var group1 = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var group2 = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        // First group — create "Whiteboard"
        var firstResponse = await client.CreateResourceSchemaAsync(group1.Id, "Whiteboard");
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<CreateRtResponse>(JsonOptions);
        Assert.NotNull(firstBody);

        // Act — same name in a different group
        var secondResponse = await client.CreateResourceSchemaAsync(group2.Id, "Whiteboard");

        // Assert — 201 Created (name scope is per-group, no collision)
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        var secondBody = await secondResponse.Content.ReadFromJsonAsync<CreateRtResponse>(JsonOptions);
        Assert.NotNull(secondBody);
        Assert.NotEqual(Guid.Empty, secondBody.Id);

        // Assert — both resource schemas exist in DB, each belonging to their own group
        var type1 = await client.GetResourceSchemaFromDbAsync(firstBody.Id);
        var type2 = await client.GetResourceSchemaFromDbAsync(secondBody.Id);

        Assert.NotNull(type1);
        Assert.Equal(group1.Id, type1.GroupId);
        Assert.Equal("Whiteboard", type1.Name);

        Assert.NotNull(type2);
        Assert.Equal(group2.Id, type2.GroupId);
        Assert.Equal("Whiteboard", type2.Name);
    }
}
