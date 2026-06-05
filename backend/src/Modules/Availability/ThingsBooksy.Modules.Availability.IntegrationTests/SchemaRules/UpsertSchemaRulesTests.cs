using System.Net;
using ThingsBooksy.Modules.Availability.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Availability.IntegrationTests.SchemaRules;

/// <summary>
/// Integration tests for PUT/GET /availability/schemas/{schemaId}/rules.
/// </summary>
[Collection("IntegrationTestCollection")]
public class UpsertSchemaRulesTests : IntegrationTestBase
{
    private readonly AvailabilityUserFactory _users;
    private readonly AvailabilityGroupFactory _groups;

    public UpsertSchemaRulesTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new AvailabilityUserFactory(factory);
        _groups = new AvailabilityGroupFactory(factory);
    }

    [Fact]
    public async Task UpsertSchemaRules_ValidRecurring_ReturnsNoContent()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("schema_upsert_recurring@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _groups.CreateSchemaReadModelAsync(group.Id);
        var client = new AvailabilityTestClient(Factory, owner);

        var body = new
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

        // Act
        var response = await client.PutSchemaRulesAsync(schema.Id, body);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var ruleSet = await client.GetSchemaRuleSetFromDbAsync(schema.Id);
        Assert.NotNull(ruleSet);
        Assert.Equal(15, ruleSet.BufferMinutes);
        Assert.Single(ruleSet.Rules);
    }

    [Fact]
    public async Task GetSchemaRules_AfterUpsert_ReturnsCorrectRules()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("schema_get_after_upsert@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _groups.CreateSchemaReadModelAsync(group.Id);
        var client = new AvailabilityTestClient(Factory, owner);

        var body = new
        {
            BufferMinutes = 30,
            Rules = new[]
            {
                new
                {
                    RuleType = "Recurring",
                    RuleMode = "Available",
                    DaysOfWeek = new[] { 1, 2, 3 },
                    StartTime = "08:00",
                    EndTime = "16:00",
                    StartDate = (string?)null,
                    EndDate = (string?)null
                }
            }
        };

        await client.PutSchemaRulesAsync(schema.Id, body);

        // Act
        var getResponse = await client.GetSchemaRulesAsync(schema.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var result = await client.ReadRulesResponseAsync(getResponse);
        Assert.NotNull(result);
        Assert.Equal(30, result.BufferMinutes);
        Assert.Single(result.Rules);
        Assert.Null(result.InheritedRules);

        var rule = result.Rules[0];
        Assert.Equal("Recurring", rule.RuleType);
        Assert.Equal("Available", rule.RuleMode);
        Assert.Equal("08:00", rule.StartTime);
        Assert.Equal("16:00", rule.EndTime);
        Assert.Equal(0, rule.EndDayOffset);
    }

    [Fact]
    public async Task UpsertSchemaRules_AvailableOverlapConflict_ReturnsBadRequest()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("schema_conflict@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _groups.CreateSchemaReadModelAsync(group.Id);
        var client = new AvailabilityTestClient(Factory, owner);

        var body = new
        {
            BufferMinutes = 0,
            Rules = new[]
            {
                new
                {
                    RuleType = "Recurring",
                    RuleMode = "Available",
                    DaysOfWeek = new[] { 1, 2 },
                    StartTime = "09:00",
                    EndTime = "17:00",
                    StartDate = (string?)null,
                    EndDate = (string?)null
                },
                new
                {
                    RuleType = "Recurring",
                    RuleMode = "Available",
                    DaysOfWeek = new[] { 2, 3 },
                    StartTime = "10:00",
                    EndTime = "18:00",
                    StartDate = (string?)null,
                    EndDate = (string?)null
                }
            }
        };

        // Act
        var response = await client.PutSchemaRulesAsync(schema.Id, body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpsertSchemaRules_OvernightRule_AutoSetsEndDayOffset()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("schema_overnight@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _groups.CreateSchemaReadModelAsync(group.Id);
        var client = new AvailabilityTestClient(Factory, owner);

        // StartTime=22:00, EndTime=06:00 → EndTime < StartTime → overnight → endDayOffset = 1
        var body = new
        {
            BufferMinutes = 0,
            Rules = new[]
            {
                new
                {
                    RuleType = "Recurring",
                    RuleMode = "Available",
                    DaysOfWeek = new[] { 1, 2, 3, 4, 5 },
                    StartTime = "22:00",
                    EndTime = "06:00",
                    StartDate = (string?)null,
                    EndDate = (string?)null
                }
            }
        };

        // Act
        var putResponse = await client.PutSchemaRulesAsync(schema.Id, body);
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        // Assert via GET — the serialized response must contain endDayOffset = 1
        var getResponse = await client.GetSchemaRulesAsync(schema.Id);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var result = await client.ReadRulesResponseAsync(getResponse);
        Assert.NotNull(result);
        Assert.Single(result.Rules);

        var rule = result.Rules[0];
        Assert.Equal("22:00", rule.StartTime);
        Assert.Equal("06:00", rule.EndTime);
        Assert.Equal(1, rule.EndDayOffset);
    }

    [Fact]
    public async Task UpsertSchemaRules_UnavailableOverlap_ReturnsNoContent()
    {
        // Arrange — overlapping Unavailable rules are allowed
        var owner = await _users.CreateUserAsync("schema_unavail_overlap@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _groups.CreateSchemaReadModelAsync(group.Id);
        var client = new AvailabilityTestClient(Factory, owner);

        var body = new
        {
            BufferMinutes = 0,
            Rules = new[]
            {
                new
                {
                    RuleType = "Recurring",
                    RuleMode = "Unavailable",
                    DaysOfWeek = new[] { 0, 6 },
                    StartTime = "00:00",
                    EndTime = "23:59",
                    StartDate = (string?)null,
                    EndDate = (string?)null
                },
                new
                {
                    RuleType = "Recurring",
                    RuleMode = "Unavailable",
                    DaysOfWeek = new[] { 0 },
                    StartTime = "08:00",
                    EndTime = "12:00",
                    StartDate = (string?)null,
                    EndDate = (string?)null
                }
            }
        };

        // Act
        var response = await client.PutSchemaRulesAsync(schema.Id, body);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpsertSchemaRules_EmptyRules_ClearsAllRules()
    {
        // Arrange — first insert rules, then clear them
        var owner = await _users.CreateUserAsync("schema_clear_rules@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var schema = await _groups.CreateSchemaReadModelAsync(group.Id);
        var client = new AvailabilityTestClient(Factory, owner);

        var populateBody = new
        {
            BufferMinutes = 10,
            Rules = new[]
            {
                new
                {
                    RuleType = "Recurring",
                    RuleMode = "Available",
                    DaysOfWeek = new[] { 1 },
                    StartTime = "09:00",
                    EndTime = "17:00",
                    StartDate = (string?)null,
                    EndDate = (string?)null
                }
            }
        };

        await client.PutSchemaRulesAsync(schema.Id, populateBody);

        var clearBody = new { BufferMinutes = 0, Rules = Array.Empty<object>() };

        // Act
        var response = await client.PutSchemaRulesAsync(schema.Id, clearBody);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await client.GetSchemaRulesAsync(schema.Id);
        var result = await client.ReadRulesResponseAsync(getResponse);
        Assert.NotNull(result);
        Assert.Empty(result.Rules);
    }
}
