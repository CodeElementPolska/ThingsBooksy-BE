using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using ThingsBooksy.Modules.Availability.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Availability.IntegrationTests.Authorization;

/// <summary>
/// Integration tests verifying that PUT /availability/schemas/{schemaId}/rules enforces
/// authentication (401) and group-ownership authorization (403).
/// </summary>
[Collection("IntegrationTestCollection")]
public class AvailabilityAuthorizationTests : IntegrationTestBase
{
    private readonly AvailabilityUserFactory _users;
    private readonly AvailabilityGroupFactory _groups;

    public AvailabilityAuthorizationTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new AvailabilityUserFactory(factory);
        _groups = new AvailabilityGroupFactory(factory);
    }

    [Fact]
    public async Task UpsertSchemaRules_Unauthenticated_Returns401()
    {
        // Arrange — create a schema so there is a valid schemaId to target
        var owner = await _users.CreateUserAsync("auth_unauth_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _groups.CreateSchemaReadModelAsync(group.Id);

        // Use an anonymous (no token) HTTP client
        var anonymousClient = Factory.CreateClient();

        var body = new
        {
            BufferMinutes = 0,
            Rules = Array.Empty<object>()
        };

        // Act
        var response = await anonymousClient.PutAsJsonAsync(
            $"/availability/schemas/{schema.Id}/rules", body);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpsertSchemaRules_NonOwner_Returns403()
    {
        // Arrange — owner creates the group/schema; non-owner attempts to PUT rules
        var owner = await _users.CreateUserAsync("auth_owner_403@test.com");
        var nonOwner = await _users.CreateUserAsync("auth_nonowner_403@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _groups.CreateSchemaReadModelAsync(group.Id);

        var nonOwnerClient = new AvailabilityTestClient(Factory, nonOwner);

        var body = new
        {
            BufferMinutes = 0,
            Rules = Array.Empty<object>()
        };

        // Act
        var response = await nonOwnerClient.PutSchemaRulesAsync(schema.Id, body);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
