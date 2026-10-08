using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceSchemas;

/// <summary>
/// Integration tests for GET /resources/schemas/{id} and GET /resources/schemas?groupId={id} (T036–T037).
/// </summary>
[Collection("IntegrationTestCollection")]
public class GetResourceSchemaTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;

    // Local response records — do not import from Core to keep test project isolated
    private record ResourceSchemaDtoResponse(Guid Id, Guid GroupId, string Name, string? Description, DateTime CreatedAt, List<PropertyDefinitionDtoResponse> PropertyDefinitions);
    private record PropertyDefinitionDtoResponse(Guid Id, string Name, string DataType, bool IsRequired);
    private record ResourceSchemasListResponse(List<ResourceSchemaDtoResponse> Items);

    public GetResourceSchemaTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/schemas/{id} — happy path
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceSchema_AsOwner_Returns200WithCorrectData()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("getrt_happy_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        var typeId = await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Desk", null,
            new[] { new PropertyDefinitionRequest("Color", (int)PropertyDataType.Text, true) });

        // Act
        var response = await client.GetResourceSchemaAsync(typeId);

        // Assert — status
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert — body shape and values
        var body = await response.Content.ReadFromJsonAsync<ResourceSchemaDtoResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(typeId, body.Id);
        Assert.Equal("Desk", body.Name);
        Assert.Equal(group.Id, body.GroupId);
        Assert.Single(body.PropertyDefinitions);
        Assert.Equal("Color", body.PropertyDefinitions[0].Name);
        Assert.Equal("Text", body.PropertyDefinitions[0].DataType);
        Assert.True(body.PropertyDefinitions[0].IsRequired);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/schemas — happy path list (2 schemas)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceSchemas_AsOwner_Returns200WithAllTypesInGroup()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("getrtlist_happy_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Chair");
        await client.CreateResourceSchemaAndGetIdAsync(group.Id, "Table");

        // Act
        var response = await client.GetResourceSchemasAsync(group.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ResourceSchemaDtoResponse>>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(2, body.Count);

        var names = body.ConvertAll(t => t.Name);
        Assert.Contains("Chair", names);
        Assert.Contains("Table", names);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/schemas/{id} — 404 when schema does not exist
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceSchema_WhenTypeDoesNotExist_Returns404()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("getrt_notfound@test.com");
        var client = new ResourcesTestClient(Factory, owner);

        var unknownId = Guid.CreateVersion7();

        // Act
        var response = await client.GetResourceSchemaAsync(unknownId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/schemas/{id} — 401 when no JWT
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceSchema_WithoutJwt_Returns401()
    {
        // Arrange — unauthenticated client
        var anonClient = ResourcesTestClient.Anonymous(Factory);
        var unknownId = Guid.CreateVersion7();

        // Act
        var response = await anonClient.GetResourceSchemaAsync(unknownId);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/schemas — 401 when no JWT
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceSchemas_WithoutJwt_Returns401()
    {
        // Arrange — unauthenticated client
        var anonClient = ResourcesTestClient.Anonymous(Factory);

        // Act
        var response = await anonClient.GetResourceSchemasAsync(Guid.CreateVersion7());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/schemas/{id} — 403 when user is not owner or member
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceSchema_AsNonMember_Returns403()
    {
        // Arrange — resource schema belongs to a group owned by someone else
        var owner = await _users.CreateUserAsync("getrt_403_owner@test.com");
        var nonMember = await _users.CreateUserAsync("getrt_403_nonmember@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);

        var ownerClient = new ResourcesTestClient(Factory, owner);
        var typeId = await ownerClient.CreateResourceSchemaAndGetIdAsync(group.Id, "Bookshelf");

        var nonMemberClient = new ResourcesTestClient(Factory, nonMember);

        // Act
        var response = await nonMemberClient.GetResourceSchemaAsync(typeId);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/schemas — 403 when user is not owner or member
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceSchemas_AsNonMember_Returns403()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("getrtlist_403_owner@test.com");
        var nonMember = await _users.CreateUserAsync("getrtlist_403_nonmember@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);

        var nonMemberClient = new ResourcesTestClient(Factory, nonMember);

        // Act
        var response = await nonMemberClient.GetResourceSchemasAsync(group.Id);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/schemas/{id} — 200 when requester is a group member (not owner)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceSchema_AsMember_Returns200()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("getrt_member_owner@test.com");
        var member = await _users.CreateUserAsync("getrt_member_user@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _groups.AddGroupMemberAsync(group.Id, member.UserId);

        var ownerClient = new ResourcesTestClient(Factory, owner);
        var typeId = await ownerClient.CreateResourceSchemaAndGetIdAsync(group.Id, "Whiteboard");

        var memberClient = new ResourcesTestClient(Factory, member);

        // Act
        var response = await memberClient.GetResourceSchemaAsync(typeId);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ResourceSchemaDtoResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(typeId, body.Id);
    }

    // -----------------------------------------------------------------------------------------
    // GET /resources/schemas — 200 when requester is a group member (not owner)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GetResourceSchemas_AsMember_Returns200()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("getrtlist_member_owner@test.com");
        var member = await _users.CreateUserAsync("getrtlist_member_user@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _groups.AddGroupMemberAsync(group.Id, member.UserId);

        var ownerClient = new ResourcesTestClient(Factory, owner);
        await ownerClient.CreateResourceSchemaAndGetIdAsync(group.Id, "Projector");

        var memberClient = new ResourcesTestClient(Factory, member);

        // Act
        var response = await memberClient.GetResourceSchemasAsync(group.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<ResourceSchemaDtoResponse>>(JsonOptions);
        Assert.NotNull(body);
        Assert.Single(body);
        Assert.Equal("Projector", body[0].Name);
    }
}
