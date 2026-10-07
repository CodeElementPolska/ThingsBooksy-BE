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

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceInstances;

/// <summary>
/// Integration tests for cursor-paginated GET /resources/instances (T058, T059, T060).
///
/// Seed 25 instances and verify that the endpoint correctly pages through them using
/// the afterId + take cursor pattern. NextCursor is null only after the last page.
/// </summary>
[Collection("IntegrationTestCollection")]
public class GetResourceInstancesEndpointTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Local response records — test-only, no import from Core
    private record RowDto(Guid Id, string Name);
    private record PagedResponse(List<RowDto> Items, Guid? NextCursor);

    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;

    public GetResourceInstancesEndpointTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // Shared seed helper — creates owner, group, type and N instances; returns the type ID
    // -----------------------------------------------------------------------------------------

    private async Task<(ResourcesTestClient Client, Guid TypeId)> SeedInstancesAsync(
        string emailPrefix, int count)
    {
        var owner = await _users.CreateUserAsync($"{emailPrefix}_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, $"{emailPrefix}_Type");

        for (var i = 1; i <= count; i++)
        {
            var created = await client.CreateResourceInstanceAsync(typeId, $"{emailPrefix}_Item_{i:D3}");
            created.EnsureSuccessStatusCode();
        }

        return (client, typeId);
    }

    // -----------------------------------------------------------------------------------------
    // T058 — First page with take=20 returns exactly 20 items and a non-null NextCursor
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceInstances_FirstPage_ReturnsAtMostTake()
    {
        // Arrange — seed 25 instances
        var (client, typeId) = await SeedInstancesAsync("t058", count: 25);

        // Act — request first page with take=20
        var response = await client.GetResourceInstancesAsync(resourceSchemaId: typeId, take: 20);

        // Assert — 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse>(JsonOptions);
        Assert.NotNull(body);

        // Assert — exactly 20 items (clamped to take)
        Assert.Equal(20, body.Items.Count);

        // Assert — cursor is set because there are more items
        Assert.NotNull(body.NextCursor);
    }

    // -----------------------------------------------------------------------------------------
    // T059 — Paging through all 25 instances produces no duplicates and totals 25
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceInstances_CursorPagination_NoDuplicatesAcrossPages()
    {
        // Arrange — seed 25 instances
        var (client, typeId) = await SeedInstancesAsync("t059", count: 25);

        // Act — page through all instances with take=20
        var allIds = new List<Guid>();
        Guid? cursor = null;

        do
        {
            var response = await client.GetResourceInstancesAsync(
                resourceSchemaId: typeId, take: 20, afterId: cursor);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var page = await response.Content.ReadFromJsonAsync<PagedResponse>(JsonOptions);
            Assert.NotNull(page);

            allIds.AddRange(page.Items.Select(i => i.Id));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        // Assert — all 25 returned across both pages
        Assert.Equal(25, allIds.Count);

        // Assert — no duplicates
        Assert.Equal(25, allIds.Distinct().Count());
    }

    // -----------------------------------------------------------------------------------------
    // T060 — After all items are returned the trailing page has an empty Items list and null cursor
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceInstances_EmptyTrailingPage_ReturnsNullCursor()
    {
        // Arrange — seed 25 instances; retrieve the cursor pointing past the 25th item
        var (client, typeId) = await SeedInstancesAsync("t060", count: 25);

        // First page: take=20, cursor=null → Items[20], NextCursor = ID of item 20
        var firstResponse = await client.GetResourceInstancesAsync(resourceSchemaId: typeId, take: 20);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<PagedResponse>(JsonOptions);
        Assert.NotNull(firstPage);
        Assert.Equal(20, firstPage.Items.Count);
        var cursorAfterFirst = firstPage.NextCursor;
        Assert.NotNull(cursorAfterFirst);

        // Second page: take=20, cursor=ID of item 20 → Items[5], NextCursor=null
        var secondResponse = await client.GetResourceInstancesAsync(
            resourceSchemaId: typeId, take: 20, afterId: cursorAfterFirst);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<PagedResponse>(JsonOptions);
        Assert.NotNull(secondPage);
        Assert.Equal(5, secondPage.Items.Count);
        // The second page has fewer items than take → no more pages
        Assert.Null(secondPage.NextCursor);

        // Act — request a third page using the last item's ID from the second page as cursor
        var lastItemId = secondPage.Items[^1].Id;
        var thirdResponse = await client.GetResourceInstancesAsync(
            resourceSchemaId: typeId, take: 20, afterId: lastItemId);
        Assert.Equal(HttpStatusCode.OK, thirdResponse.StatusCode);
        var thirdPage = await thirdResponse.Content.ReadFromJsonAsync<PagedResponse>(JsonOptions);
        Assert.NotNull(thirdPage);

        // Assert — trailing page is empty and cursor is null
        Assert.Empty(thirdPage.Items);
        Assert.Null(thirdPage.NextCursor);
    }
}
