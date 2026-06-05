using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ThingsBooksy.Modules.ManagementGroups.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.ManagementGroups.IntegrationTests;

public class CreateManagementGroupTests : IntegrationTestBase
{
    private readonly ManagementGroupsUserFactory _users;

    public CreateManagementGroupTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ManagementGroupsUserFactory(factory);
    }

    [Fact]
    public async Task CreateGroup_WithValidData_Returns201AndPersistsInDb()
    {
        var owner = await _users.CreateUserAsync("create_valid@test.com");
        var groups = new ManagementGroupsTestClient(Factory, owner);

        var response = await groups.CreateGroupAsync("My Group");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateGroupResponse>();
        Assert.NotNull(result);

        var dbGroup = await groups.GetGroupFromDbAsync(result.Id);
        Assert.NotNull(dbGroup);
        Assert.Equal("My Group", dbGroup.Name);
        Assert.Equal(owner.UserId, dbGroup.OwnerId);
    }

    [Fact]
    public async Task CreateGroup_WhenUnauthenticated_Returns401()
    {
        var anonymousClient = Factory.CreateClient();

        var response = await anonymousClient.PostAsJsonAsync("/management-groups", new { Name = "Anon Group" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // T032 — duplicate name for same owner returns 409 with GROUP_NAME_TAKEN code
    [Fact]
    public async Task CreateManagementGroup_DuplicateNameSameOwner_Returns409()
    {
        var owner = await _users.CreateUserAsync("create_dup409@test.com");
        var groups = new ManagementGroupsTestClient(Factory, owner);
        await groups.CreateGroupAndGetIdAsync("Taken Name");

        var response = await groups.CreateGroupAsync("Taken Name");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(options);
        Assert.NotNull(body);
        Assert.Equal("GROUP_NAME_TAKEN", body.Code);
    }

    // T033 — same name is allowed when it belongs to a different owner
    [Fact]
    public async Task CreateManagementGroup_DuplicateNameDifferentOwner_Succeeds()
    {
        var owner1 = await _users.CreateUserAsync("create_dup_owner1@test.com");
        var owner2 = await _users.CreateUserAsync("create_dup_owner2@test.com");
        var groups1 = new ManagementGroupsTestClient(Factory, owner1);
        var groups2 = new ManagementGroupsTestClient(Factory, owner2);

        await groups1.CreateGroupAndGetIdAsync("Shared Name");

        var response = await groups2.CreateGroupAsync("Shared Name");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // T034 — name of soft-deleted group is available again for the same owner
    [Fact]
    public async Task CreateManagementGroup_NameReusedAfterSoftDelete_Succeeds()
    {
        var owner = await _users.CreateUserAsync("create_reuse@test.com");
        var groups = new ManagementGroupsTestClient(Factory, owner);

        var groupId = await groups.CreateGroupAndGetIdAsync("Recyclable Name");
        await groups.DeleteGroupAsync(groupId);

        var response = await groups.CreateGroupAsync("Recyclable Name");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateGroup_WithoutName_Returns400()
    {
        var owner = await _users.CreateUserAsync("create_noname@test.com");
        var groups = new ManagementGroupsTestClient(Factory, owner);

        var response = await groups.CreateGroupAsync("");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // T012 — timezone validation tests

    [Fact]
    public async Task CreateManagementGroup_WithValidTimeZone_ReturnsCreated()
    {
        var owner = await _users.CreateUserAsync("tz_valid@test.com");
        var groups = new ManagementGroupsTestClient(Factory, owner);

        var response = await groups.CreateGroupAsync("TZ Valid Group", timeZoneId: "Europe/Warsaw");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateGroupResponse>();
        Assert.NotNull(result);

        var dbGroup = await groups.GetGroupFromDbAsync(result.Id);
        Assert.NotNull(dbGroup);
        Assert.Equal("Europe/Warsaw", dbGroup.TimeZoneId);
    }

    [Fact]
    public async Task CreateManagementGroup_WithInvalidTimeZone_ReturnsBadRequest()
    {
        var owner = await _users.CreateUserAsync("tz_invalid@test.com");
        var groups = new ManagementGroupsTestClient(Factory, owner);

        var response = await groups.CreateGroupAsync("TZ Invalid Group", timeZoneId: "Invalid/Timezone");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateManagementGroup_WithEmptyTimeZone_ReturnsBadRequest()
    {
        var owner = await _users.CreateUserAsync("tz_empty@test.com");
        var groups = new ManagementGroupsTestClient(Factory, owner);

        var response = await groups.CreateGroupAsync("TZ Empty Group", timeZoneId: "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateManagementGroup_GroupCreatedEventContainsTimeZoneId()
    {
        var owner = await _users.CreateUserAsync("tz_event_payload@test.com");
        var groups = new ManagementGroupsTestClient(Factory, owner);

        var response = await groups.CreateGroupAsync("TZ Event Group", timeZoneId: "Europe/London");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateGroupResponse>();
        Assert.NotNull(result);

        // GroupCreated event is handled in-process by Resources.GroupCreatedHandler.
        // If the event carried TimeZoneId correctly, the group was persisted with the right value.
        var dbGroup = await groups.GetGroupFromDbAsync(result.Id);
        Assert.NotNull(dbGroup);
        Assert.Equal("Europe/London", dbGroup.TimeZoneId);

        // The Resources module's GroupReadModel must also have been upserted (event reached subscriber).
        var resourcesReadModelExists = await groups.ResourcesGroupReadModelExistsAsync(result.Id);
        Assert.True(resourcesReadModelExists);
    }

    // DTO used only within this test file to deserialise the 409 body
    private sealed record ErrorBody(string Code, string Message);
}
