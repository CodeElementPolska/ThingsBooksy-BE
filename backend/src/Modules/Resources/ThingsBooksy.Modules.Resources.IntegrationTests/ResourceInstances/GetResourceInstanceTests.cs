using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceInstances;

/// <summary>
/// Integration tests for GET /resources/instances/{id} and GET /resources/instances (T038–T041).
/// </summary>
[Collection("IntegrationTestCollection")]
public class GetResourceInstanceTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;

    // Local response records — do not import from Core to keep test project isolated
    private record ResourceInstanceDtoResponse(Guid Id, Guid ResourceTypeId, Guid GroupId, string Name, string? Description, Guid OwnerId, DateTime CreatedAt, DateTime? DeletedAt, List<PropertyValueDtoResponse> PropertyValues);
    private record PropertyValueDtoResponse(Guid PropertyDefinitionId, string PropertyName, string DataType, string Value);
    private record PagedInstancesResponse(List<ResourceInstanceDtoResponse> Items, Guid? NextCursor);

    public GetResourceInstanceTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/instances/{id} — happy path with property values
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceInstance_AsOwner_Returns200WithCorrectData()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("getri_happy_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var definitions = new[]
        {
            new PropertyDefinitionRequest("Color", (int)PropertyDataType.Text, false),
            new PropertyDefinitionRequest("Seats", (int)PropertyDataType.Number, false),
        };

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Car", null, definitions);
        var storedDefs = await client.GetResourcePropertyDefinitionsFromDbAsync(typeId);
        var colorDef = storedDefs.First(d => d.Name == "Color");
        var seatsDef = storedDefs.First(d => d.Name == "Seats");

        var propertyValues = new[]
        {
            new PropertyValueRequest(colorDef.Id, "Red"),
            new PropertyValueRequest(seatsDef.Id, "5"),
        };

        var instanceId = await client.CreateResourceInstanceAndGetIdAsync(typeId, "Car One", "First car", propertyValues);

        // Act
        var response = await client.GetResourceInstanceAsync(instanceId);

        // Assert — status
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert — body shape and values
        var body = await response.Content.ReadFromJsonAsync<ResourceInstanceDtoResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(instanceId, body.Id);
        Assert.Equal("Car One", body.Name);
        Assert.Equal("First car", body.Description);
        Assert.Equal(typeId, body.ResourceTypeId);
        Assert.Equal(group.Id, body.GroupId);
        Assert.Equal(owner.UserId, body.OwnerId);
        Assert.Null(body.DeletedAt);
        Assert.Equal(2, body.PropertyValues.Count);

        var colorValue = body.PropertyValues.First(pv => pv.PropertyDefinitionId == colorDef.Id);
        Assert.Equal("Color", colorValue.PropertyName);
        Assert.Equal("Text", colorValue.DataType);
        Assert.Equal("Red", colorValue.Value);

        var seatsValue = body.PropertyValues.First(pv => pv.PropertyDefinitionId == seatsDef.Id);
        Assert.Equal("Seats", seatsValue.PropertyName);
        Assert.Equal("Number", seatsValue.DataType);
        Assert.Equal("5", seatsValue.Value);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/instances — happy path list by resourceTypeId
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceInstances_ByResourceSchemaId_Returns200WithBothInstances()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("getrilist_bytype_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Desk");
        await client.CreateResourceInstanceAndGetIdAsync(typeId, "Desk A");
        await client.CreateResourceInstanceAndGetIdAsync(typeId, "Desk B");

        // Act
        var response = await client.GetResourceInstancesAsync(resourceSchemaId: typeId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PagedInstancesResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(2, body.Items.Count);

        var names = body.Items.ConvertAll(i => i.Name);
        Assert.Contains("Desk A", names);
        Assert.Contains("Desk B", names);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/instances — soft-deleted excluded by default; visible with includeDeleted=true
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceInstances_SoftDeletedExcludedByDefault_AndVisibleWithIncludeDeleted()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("getrilist_softdelete@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Monitor");
        var instanceId = await client.CreateResourceInstanceAndGetIdAsync(typeId, "Monitor A");

        // Soft-delete via DB directly (DELETE endpoint T043 not yet implemented)
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
            var inst = await db.ResourceInstances.IgnoreQueryFilters().FirstAsync(x => x.Id == instanceId);
            inst.Delete(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        // Act — default query (excludes deleted)
        var defaultResponse = await client.GetResourceInstancesAsync(resourceSchemaId: typeId);
        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
        var defaultBody = await defaultResponse.Content.ReadFromJsonAsync<PagedInstancesResponse>(JsonOptions);
        Assert.NotNull(defaultBody);
        Assert.Empty(defaultBody.Items);

        // Act — with includeDeleted=true
        var withDeletedResponse = await client.GetResourceInstancesAsync(resourceSchemaId: typeId, includeDeleted: true);
        Assert.Equal(HttpStatusCode.OK, withDeletedResponse.StatusCode);
        var withDeletedBody = await withDeletedResponse.Content.ReadFromJsonAsync<PagedInstancesResponse>(JsonOptions);
        Assert.NotNull(withDeletedBody);
        Assert.Single(withDeletedBody.Items);
        Assert.Equal(instanceId, withDeletedBody.Items[0].Id);
        Assert.NotNull(withDeletedBody.Items[0].DeletedAt);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/instances/{id} — 404 when instance does not exist
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceInstance_WhenInstanceDoesNotExist_Returns404()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("getri_notfound@test.com");
        var client = new ResourcesTestClient(Factory, owner);

        var unknownId = Guid.CreateVersion7();

        // Act
        var response = await client.GetResourceInstanceAsync(unknownId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/instances/{id} — 401 when no JWT
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceInstance_WithoutJwt_Returns401()
    {
        // Arrange — unauthenticated client
        var anonClient = Factory.CreateClient();

        // Act
        var response = await anonClient.GetAsync($"/resources/instances/{Guid.CreateVersion7()}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/instances — 401 when no JWT
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceInstances_WithoutJwt_Returns401()
    {
        // Arrange — unauthenticated client
        var anonClient = Factory.CreateClient();

        // Act
        var response = await anonClient.GetAsync($"/resources/instances?groupId={Guid.CreateVersion7()}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/instances/{id} — 403 when user is not owner or member
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceInstance_AsNonMember_Returns403()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("getri_403_owner@test.com");
        var nonMember = await _users.CreateUserAsync("getri_403_nonmember@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);

        var ownerClient = new ResourcesTestClient(Factory, owner);
        var typeId = await ownerClient.CreateResourceSchemaAndGetIdAsync(group.Id, "Laptop");
        var instanceId = await ownerClient.CreateResourceInstanceAndGetIdAsync(typeId, "Laptop A");

        var nonMemberClient = new ResourcesTestClient(Factory, nonMember);

        // Act
        var response = await nonMemberClient.GetResourceInstanceAsync(instanceId);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/instances — 403 when user is not owner or member
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceInstances_AsNonMember_Returns403()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("getrilist_403_owner@test.com");
        var nonMember = await _users.CreateUserAsync("getrilist_403_nonmember@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);

        var ownerClient = new ResourcesTestClient(Factory, owner);
        var typeId = await ownerClient.CreateResourceSchemaAndGetIdAsync(group.Id, "Phone");

        var nonMemberClient = new ResourcesTestClient(Factory, nonMember);

        // Act
        var response = await nonMemberClient.GetResourceInstancesAsync(resourceSchemaId: typeId);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/instances/{id} — 200 when requester is a group member (not owner)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceInstance_AsMember_Returns200()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("getri_member_owner@test.com");
        var member = await _users.CreateUserAsync("getri_member_user@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _groups.AddGroupMemberAsync(group.Id, member.UserId);

        var ownerClient = new ResourcesTestClient(Factory, owner);
        var typeId = await ownerClient.CreateResourceSchemaAndGetIdAsync(group.Id, "Printer");
        var instanceId = await ownerClient.CreateResourceInstanceAndGetIdAsync(typeId, "Printer A");

        var memberClient = new ResourcesTestClient(Factory, member);

        // Act
        var response = await memberClient.GetResourceInstanceAsync(instanceId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ResourceInstanceDtoResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(instanceId, body.Id);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/instances — 200 when requester is a group member (not owner)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceInstances_AsMember_Returns200()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("getrilist_member_owner@test.com");
        var member = await _users.CreateUserAsync("getrilist_member_user@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _groups.AddGroupMemberAsync(group.Id, member.UserId);

        var ownerClient = new ResourcesTestClient(Factory, owner);
        var typeId = await ownerClient.CreateResourceSchemaAndGetIdAsync(group.Id, "Scanner");
        await ownerClient.CreateResourceInstanceAndGetIdAsync(typeId, "Scanner A");

        var memberClient = new ResourcesTestClient(Factory, member);

        // Act
        var response = await memberClient.GetResourceInstancesAsync(resourceSchemaId: typeId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedInstancesResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Single(body.Items);
        Assert.Equal("Scanner A", body.Items[0].Name);
    }
}
