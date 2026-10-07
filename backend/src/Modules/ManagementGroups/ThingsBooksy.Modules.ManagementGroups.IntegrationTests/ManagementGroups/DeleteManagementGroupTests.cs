using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ThingsBooksy.Modules.ManagementGroups.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.ManagementGroups.IntegrationTests;

public class DeleteManagementGroupTests : IntegrationTestBase
{
    private readonly ManagementGroupsUserFactory _users;

    public DeleteManagementGroupTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ManagementGroupsUserFactory(factory);
    }

    [Fact]
    public async Task DeleteGroup_AsOwner_Returns204AndSoftDeletesInDb()
    {
        var owner = await _users.CreateUserAsync("delete_owner@test.com");
        var groups = new ManagementGroupsTestClient(Factory, owner);
        var groupId = await groups.CreateGroupAndGetIdAsync("Group to Delete");

        var response = await groups.DeleteGroupAsync(groupId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var dbGroup = await groups.GetGroupFromDbAsync(groupId);
        Assert.NotNull(dbGroup);
        Assert.NotNull(dbGroup.DeletedAt);
    }

    [Fact]
    public async Task DeleteGroup_AsNonOwner_Returns403()
    {
        var owner = await _users.CreateUserAsync("delete_owner2@test.com");
        var ownerGroups = new ManagementGroupsTestClient(Factory, owner);
        var groupId = await ownerGroups.CreateGroupAndGetIdAsync("Protected Group");

        var other = await _users.CreateUserAsync("delete_other@test.com");
        var otherGroups = new ManagementGroupsTestClient(Factory, other);

        var response = await otherGroups.DeleteGroupAsync(groupId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteGroup_MakesGroupInvisibleInGetRequests()
    {
        var owner = await _users.CreateUserAsync("delete_invisible@test.com");
        var groups = new ManagementGroupsTestClient(Factory, owner);
        var groupId = await groups.CreateGroupAndGetIdAsync("Invisible After Delete");

        await groups.DeleteGroupAsync(groupId);

        var getResponse = await groups.GetGroupAsync(groupId);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // T070 — deleting a group cascades to the Resources module via the GroupDeleted event:
    //         all resource schemas are hard-deleted and all resource instances are soft-deleted.
    //
    // The cascade is driven by the AsyncDispatcherJob in-process background service;
    // assertions poll until the side-effect is visible or a 3-second deadline elapses —
    // the same pattern used by EventPublishingTests.
    [Fact]
    public async Task DeleteManagementGroup_CascadesToResourceInstancesAndSchemas()
    {
        var owner = await _users.CreateUserAsync("delete_cascade_owner@test.com");
        var ownerClient = new ManagementGroupsTestClient(Factory, owner);

        // Arrange — create group; wait for GroupCreated event to replicate to Resources
        var groupId = await ownerClient.CreateGroupAndGetIdAsync("Cascade Test Group");

        var groupReadModelCreated = await WaitUntilAsync(
            () => ownerClient.ResourcesGroupReadModelExistsAsync(groupId),
            maxAttempts: 50);
        Assert.True(groupReadModelCreated,
            "Pre-condition failed: Resources.GroupReadModel not populated after group creation.");

        // Arrange — create 2 resource schemas and 3 resource instances via the Resources API
        // (use factory's HTTP client which carries the owner's JWT)
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var schemaAResponse = await owner.Client.PostAsJsonAsync("/resources/types", new
        {
            GroupId = groupId,
            Name = "Camera Type",
            Description = (string?)null,
            PropertyDefinitions = Array.Empty<object>()
        });
        schemaAResponse.EnsureSuccessStatusCode();
        var schemaAResult = await schemaAResponse.Content.ReadFromJsonAsync<ResourceIdResult>(jsonOptions);
        var schemaAId = schemaAResult!.Id;

        var schemaBResponse = await owner.Client.PostAsJsonAsync("/resources/types", new
        {
            GroupId = groupId,
            Name = "Laptop Type",
            Description = (string?)null,
            PropertyDefinitions = Array.Empty<object>()
        });
        schemaBResponse.EnsureSuccessStatusCode();

        // Create 3 instances of schema A
        for (var i = 1; i <= 3; i++)
        {
            var instanceResponse = await owner.Client.PostAsJsonAsync("/resources/instances", new
            {
                ResourceTypeId = schemaAId,
                Name = $"Camera {i}",
                Description = (string?)null,
                PropertyValues = Array.Empty<object>()
            });
            instanceResponse.EnsureSuccessStatusCode();
        }

        // Verify pre-condition: 2 schemas and 3 instances exist in Resources DB
        var schemasBeforeDelete = await ownerClient.ResourcesAllResourceSchemasDeletedAsync(groupId);
        Assert.False(schemasBeforeDelete, "Pre-condition: resource schemas should exist before delete.");
        var instanceCount = await ownerClient.ResourcesResourceInstanceCountAsync(groupId);
        Assert.Equal(3, instanceCount);

        // Act — delete the management group
        var deleteResponse = await ownerClient.DeleteGroupAsync(groupId);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Assert — GroupDeleted event cascades through Resources.GroupDeletedHandler
        // which hard-deletes all resource schemas and soft-deletes all resource instances.
        // Allow up to 5 seconds (50 * 100 ms) — the async dispatcher can take slightly longer
        // when the factory is cold or when several resource rows are involved.

        var allSchemasDeleted = await WaitUntilAsync(
            () => ownerClient.ResourcesAllResourceSchemasDeletedAsync(groupId),
            maxAttempts: 50);
        Assert.True(allSchemasDeleted,
            "Resources.ResourceSchemas were not hard-deleted after GroupDeleted event — cascade failed.");

        var allInstancesSoftDeleted = await WaitUntilAsync(
            () => ownerClient.ResourcesAllResourceInstancesSoftDeletedAsync(groupId),
            maxAttempts: 50);
        Assert.True(allInstancesSoftDeleted,
            "Resources.ResourceInstances were not soft-deleted after GroupDeleted event — cascade failed.");

        // The raw instance rows still exist (soft-delete), but total count is unchanged
        var instanceCountAfterDelete = await ownerClient.ResourcesResourceInstanceCountAsync(groupId);
        Assert.Equal(3, instanceCountAfterDelete);
    }

    // Polling helper — retries the condition every 100 ms for up to 3 seconds
    private static async Task<bool> WaitUntilAsync(
        Func<Task<bool>> condition,
        int maxAttempts = 30,
        int intervalMs = 100)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            if (await condition())
                return true;

            await Task.Delay(intervalMs);
        }

        return false;
    }

    // DTO for deserialising created resource id
    private sealed record ResourceIdResult(Guid Id);
}

