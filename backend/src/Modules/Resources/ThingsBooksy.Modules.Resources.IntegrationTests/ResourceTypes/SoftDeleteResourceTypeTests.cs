using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceTypes;

/// <summary>
/// Acceptance tests for the schema soft delete (story 015, User Story 2: AC-8, AC-9).
///
/// Arrange = EF seeding through factories, Act = HTTP, Assert = DB re-read (with and without
/// IgnoreQueryFilters) and the read endpoints.
/// </summary>
[Collection("IntegrationTestCollection")]
public class SoftDeleteResourceTypeTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record ResourceTypeListItem(Guid Id, Guid GroupId, string Name);

    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;
    private readonly ResourcesResourceTypeFactory _types;
    private readonly ResourcesResourceInstanceFactory _instances;

    public SoftDeleteResourceTypeTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
        _types = new ResourcesResourceTypeFactory(factory);
        _instances = new ResourcesResourceInstanceFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // AC-8 — DELETE /resources/types/{id}: 204, row kept with DeletedAt, hidden from every read path,
    //        instances soft-deleted
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "015/AC-8")]
    public async Task DeleteResourceType_AsOwnerWithInstances_SoftDeletesTypeAndHidesIt()
    {
        // Arrange — schema with 2 instances plus a second, untouched schema in the same group
        var owner = await _users.CreateUserAsync("softdelrt_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var resourceType = await _types.CreateResourceTypeAsync(group.Id, owner.UserId, "Boat", "To be soft-deleted");
        var otherType = await _types.CreateResourceTypeAsync(group.Id, owner.UserId, "Trailer");
        var instanceA = await _instances.CreateResourceInstanceAsync(resourceType, owner.UserId, "Boat A");
        var instanceB = await _instances.CreateResourceInstanceAsync(resourceType, owner.UserId, "Boat B");
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.DeleteResourceTypeAsync(resourceType.Id);

        // Assert — 204 No Content
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Assert — the row remains in resources.resource_types with DeletedAt set (IgnoreQueryFilters)
        var rowIgnoringFilters = await client.GetResourceTypeFromDbIgnoringFiltersAsync(resourceType.Id);
        Assert.NotNull(rowIgnoringFilters);
        Assert.NotNull(rowIgnoringFilters.DeletedAt);
        Assert.Equal("Boat", rowIgnoringFilters.Name);
        Assert.Equal(group.Id, rowIgnoringFilters.GroupId);

        // Assert — the row is visible ONLY with IgnoreQueryFilters
        var rowRespectingFilters = await client.GetResourceTypeFromDbRespectingQueryFiltersAsync(resourceType.Id);
        Assert.Null(rowRespectingFilters);

        // Assert — GET /resources/types/{id} returns 404
        var getResponse = await client.GetResourceTypeAsync(resourceType.Id);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        // Assert — GET /resources/types?groupId= no longer lists it (the other schema is still listed)
        var listResponse = await client.GetResourceTypesAsync(group.Id);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<List<ResourceTypeListItem>>(JsonOptions);
        Assert.NotNull(list);
        Assert.DoesNotContain(list, t => t.Id == resourceType.Id);
        Assert.Contains(list, t => t.Id == otherType.Id);

        // Assert — its instances are soft-deleted (rows kept, DeletedAt set)
        var instances = await client.GetInstancesFromDbIgnoringFiltersAsync(resourceType.Id);
        Assert.Equal(2, instances.Count);
        Assert.Contains(instances, i => i.Id == instanceA.Id);
        Assert.Contains(instances, i => i.Id == instanceB.Id);
        Assert.All(instances, i => Assert.NotNull(i.DeletedAt));

        // Assert — the other schema is untouched
        var otherRow = await client.GetResourceTypeFromDbIgnoringFiltersAsync(otherType.Id);
        Assert.NotNull(otherRow);
        Assert.Null(otherRow.DeletedAt);
    }

    // -----------------------------------------------------------------------------------------
    // AC-9 — the name of a soft-deleted schema can be reused in the same group (201 Created)
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "015/AC-9")]
    public async Task CreateResourceType_WithNameOfSoftDeletedType_Returns201AndKeepsDeletedRow()
    {
        // Arrange — schema "Sauna" in group G, deleted by the owner through the delete command
        var owner = await _users.CreateUserAsync("softdelrt_reuse_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var original = await _types.CreateResourceTypeAsync(group.Id, owner.UserId, "Sauna");
        var client = new ResourcesTestClient(Factory, owner);

        var deleteResponse = await client.DeleteResourceTypeAsync(original.Id);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Act — the owner creates a new schema with the same name in the same group
        var response = await client.CreateResourceTypeAsync(group.Id, "Sauna");

        // Assert — 201 Created with a new id
        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 but got {response.StatusCode}. Body: {rawBody}");
        var body = JsonSerializer.Deserialize<CreateResourceTypeResponse>(rawBody, JsonOptions);
        Assert.NotNull(body);
        Assert.NotEqual(original.Id, body.Id);

        // Assert — the DB holds exactly two "Sauna" rows in G: the soft-deleted original and the new active one
        var rows = await client.GetResourceTypesByGroupAndNameFromDbIgnoringFiltersAsync(group.Id, "Sauna");
        Assert.Equal(2, rows.Count);

        var deletedRow = Assert.Single(rows, r => r.Id == original.Id);
        Assert.NotNull(deletedRow.DeletedAt);

        var activeRow = Assert.Single(rows, r => r.Id == body.Id);
        Assert.Null(activeRow.DeletedAt);
        Assert.Single(rows, r => r.DeletedAt is null);
    }
}
