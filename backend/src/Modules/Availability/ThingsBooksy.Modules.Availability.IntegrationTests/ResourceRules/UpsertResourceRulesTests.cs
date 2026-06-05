using System.Net;
using ThingsBooksy.Modules.Availability.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Availability.IntegrationTests.ResourceRules;

/// <summary>
/// Integration tests for PUT/GET /availability/resources/{resourceId}/rules.
/// </summary>
[Collection("IntegrationTestCollection")]
public class UpsertResourceRulesTests : IntegrationTestBase
{
    private readonly AvailabilityUserFactory _users;
    private readonly AvailabilityGroupFactory _groups;

    public UpsertResourceRulesTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new AvailabilityUserFactory(factory);
        _groups = new AvailabilityGroupFactory(factory);
    }

    [Fact]
    public async Task UpsertResourceRules_WithOverride_ReturnsNoContent()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("resource_upsert_override@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _groups.CreateSchemaReadModelAsync(group.Id, defaultBufferMinutes: 10);
        var resource = await _groups.CreateResourceReadModelAsync(schema.Id);
        var client = new AvailabilityTestClient(Factory, owner);

        var body = new
        {
            BufferMinutes = 20,
            Rules = new[]
            {
                new
                {
                    RuleType = "Recurring",
                    RuleMode = "Available",
                    DaysOfWeek = new[] { 1, 2, 3, 4, 5 },
                    StartTime = "10:00",
                    EndTime = "18:00",
                    StartDate = (string?)null,
                    EndDate = (string?)null
                }
            }
        };

        // Act
        var response = await client.PutResourceRulesAsync(resource.Id, body);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var ruleSet = await client.GetResourceRuleSetFromDbAsync(resource.Id);
        Assert.NotNull(ruleSet);
        Assert.Equal(20, ruleSet.BufferMinutesOverride);
        Assert.Single(ruleSet.Rules);
    }

    [Fact]
    public async Task GetResourceRules_ReturnsInheritedAndOwnRules()
    {
        // Arrange — set schema rules first, then resource-level rules
        var owner = await _users.CreateUserAsync("resource_get_inherited@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _groups.CreateSchemaReadModelAsync(group.Id, defaultBufferMinutes: 5);
        var resource = await _groups.CreateResourceReadModelAsync(schema.Id);
        var client = new AvailabilityTestClient(Factory, owner);

        // Set schema-level rules
        var schemaBody = new
        {
            BufferMinutes = 15,
            Rules = new[]
            {
                new
                {
                    RuleType = "Recurring",
                    RuleMode = "Available",
                    DaysOfWeek = new[] { 1, 2, 3, 4, 5 },
                    StartTime = "09:00",
                    EndTime = "17:00",
                    StartDate = (string?)null,
                    EndDate = (string?)null
                }
            }
        };
        await client.PutSchemaRulesAsync(schema.Id, schemaBody);

        // Set resource-level rules
        var resourceBody = new
        {
            BufferMinutes = 30,
            Rules = new[]
            {
                new
                {
                    RuleType = "Recurring",
                    RuleMode = "Unavailable",
                    DaysOfWeek = new[] { 3 },
                    StartTime = "12:00",
                    EndTime = "13:00",
                    StartDate = (string?)null,
                    EndDate = (string?)null
                }
            }
        };
        await client.PutResourceRulesAsync(resource.Id, resourceBody);

        // Act
        var getResponse = await client.GetResourceRulesAsync(resource.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var result = await client.ReadRulesResponseAsync(getResponse);
        Assert.NotNull(result);
        Assert.Equal(30, result.BufferMinutes);
        Assert.Single(result.Rules);
        Assert.NotNull(result.InheritedRules);
        Assert.Single(result.InheritedRules);

        var ownRule = result.Rules[0];
        Assert.Equal("Unavailable", ownRule.RuleMode);
        Assert.Equal("12:00", ownRule.StartTime);

        var inheritedRule = result.InheritedRules[0];
        Assert.Equal("Available", inheritedRule.RuleMode);
        Assert.Equal("09:00", inheritedRule.StartTime);
    }

    [Fact]
    public async Task UpsertResourceRules_NullBufferMinutes_InheritsSchemaBuffer()
    {
        // Arrange — create a schema with BufferMinutes=30, then PUT resource rules with bufferMinutes: null
        var owner = await _users.CreateUserAsync("resource_inherit_buffer@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _groups.CreateSchemaReadModelAsync(group.Id, defaultBufferMinutes: 30);
        var resource = await _groups.CreateResourceReadModelAsync(schema.Id);
        var client = new AvailabilityTestClient(Factory, owner);

        // First set schema-level buffer to 30 via schema rule upsert
        var schemaBody = new
        {
            BufferMinutes = 30,
            Rules = Array.Empty<object>()
        };
        await client.PutSchemaRulesAsync(schema.Id, schemaBody);

        // PUT resource rules with null bufferMinutes (= inherit from schema)
        var body = new
        {
            BufferMinutes = (int?)null,
            Rules = Array.Empty<object>()
        };

        // Act
        var putResponse = await client.PutResourceRulesAsync(resource.Id, body);
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        // Assert — GET must return bufferMinutes = 30 (inherited from schema's rule set buffer)
        var getResponse = await client.GetResourceRulesAsync(resource.Id);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var result = await client.ReadRulesResponseAsync(getResponse);
        Assert.NotNull(result);
        Assert.Equal(30, result.BufferMinutes);
    }

    [Fact]
    public async Task UpsertResourceRules_PastDate_ReturnsBadRequest()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("resource_pastdate@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _groups.CreateSchemaReadModelAsync(group.Id);
        var resource = await _groups.CreateResourceReadModelAsync(schema.Id);
        var client = new AvailabilityTestClient(Factory, owner);

        var pastDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd");

        var body = new
        {
            BufferMinutes = 0,
            Rules = new[]
            {
                new
                {
                    RuleType = "OneOff",
                    RuleMode = "Available",
                    DaysOfWeek = (int[]?)null,
                    StartTime = "09:00",
                    EndTime = "17:00",
                    StartDate = pastDate,
                    EndDate = (string?)null
                }
            }
        };

        // Act
        var response = await client.PutResourceRulesAsync(resource.Id, body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
