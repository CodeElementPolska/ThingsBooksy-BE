using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Availability.Core.DAL;
using ThingsBooksy.Modules.Availability.IntegrationTests.Clients;
using ThingsBooksy.Shared.Abstractions.EventPayloads.Calendar;
using ThingsBooksy.Shared.Abstractions.Events.Calendar;
using ThingsBooksy.Shared.Abstractions.Messaging;
using ThingsBooksy.Shared.Infrastructure.Contexts;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Availability.IntegrationTests.EventHandlers;

/// <summary>
/// Verifies that Availability event handlers correctly populate read-model tables in response
/// to integration events from ManagementGroups and Resources modules.
///
/// Events flow through HTTP endpoints → command handlers → IMessageBroker.PublishAsync →
/// AsyncDispatcherJob → Availability event handlers → AvailabilityDbContext.
///
/// Because dispatch is asynchronous, assertions poll every 100 ms for up to 6 seconds.
/// </summary>
[Collection("IntegrationTestCollection")]
public class AvailabilityEventHandlerTests : IntegrationTestBase
{
    private readonly AvailabilityUserFactory _users;

    public AvailabilityEventHandlerTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new AvailabilityUserFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // GroupCreated → GroupReadModel upserted
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GroupCreated_UpsertGroupReadModel_SetsTimeZoneId()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("avail_groupcreated@test.com");
        var anonClient = new AvailabilityTestClient(Factory, owner);

        // Act — create a group via ManagementGroups endpoint which fires GroupCreated event
        var response = await owner.Client.PostAsJsonAsync("/management-groups", new
        {
            Name = "Availability GroupCreated Test",
            Description = (string?)null,
            TimeZoneId = "Europe/Warsaw"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = await response.Content.ReadFromJsonAsync<CreateGroupResponse>(options);
        Assert.NotNull(body);

        // Assert — poll until GroupReadModel appears in availability schema.
        // Allow up to 12 seconds: OutboxProcessor has a 5 s start delay, plus 1 s processing interval,
        // plus dispatch latency — giving 6 s margin beyond the outbox warm-up.
        var populated = await WaitUntilAsync(() => anonClient.GroupReadModelExistsAsync(body.Id), maxAttempts: 120);
        Assert.True(populated, "Availability.GroupReadModel was not populated after GroupCreated event.");

        var readModel = await anonClient.GetGroupReadModelFromDbAsync(body.Id);
        Assert.NotNull(readModel);
        Assert.Equal(owner.UserId, readModel.OwnerId);
        Assert.Equal("Europe/Warsaw", readModel.TimeZoneId);
    }

    // -----------------------------------------------------------------------------------------
    // ResourceSchemaCreated → SchemaReadModel upserted
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task ResourceSchemaCreated_UpsertSchemaReadModel()
    {
        // Arrange — create group and resource type (schema) via HTTP
        var owner = await _users.CreateUserAsync("avail_schemacreated@test.com");
        var anonClient = new AvailabilityTestClient(Factory, owner);

        var groupResponse = await owner.Client.PostAsJsonAsync("/management-groups", new
        {
            Name = "Availability SchemaCreated Test",
            Description = (string?)null,
            TimeZoneId = "UTC"
        });
        groupResponse.EnsureSuccessStatusCode();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var groupBody = await groupResponse.Content.ReadFromJsonAsync<CreateGroupResponse>(options);
        Assert.NotNull(groupBody);

        // Wait for GroupReadModel in resources schema (needed for resource type creation)
        await WaitUntilAsync(async () =>
        {
            var r = await owner.Client.GetAsync($"/resources/types?groupId={groupBody.Id}");
            return r.StatusCode != HttpStatusCode.Forbidden;
        });

        // Act — create a resource type which fires ResourceSchemaCreatedEvent
        var typeResponse = await owner.Client.PostAsJsonAsync("/resources/types", new
        {
            GroupId = groupBody.Id,
            Name = "Avail Test Schema",
            Description = (string?)null,
            PropertyDefinitions = Array.Empty<object>(),
            BufferMinutes = 10
        });
        typeResponse.EnsureSuccessStatusCode();
        var typeBody = await typeResponse.Content.ReadFromJsonAsync<CreateResourceTypeResponse>(options);
        Assert.NotNull(typeBody);

        // Assert
        var populated = await WaitUntilAsync(() => anonClient.SchemaReadModelExistsAsync(typeBody.Id));
        Assert.True(populated, "Availability.SchemaReadModel was not populated after ResourceSchemaCreatedEvent.");

        var readModel = await anonClient.GetSchemaReadModelFromDbAsync(typeBody.Id);
        Assert.NotNull(readModel);
        Assert.Equal(groupBody.Id, readModel.GroupId);
    }

    // -----------------------------------------------------------------------------------------
    // ResourceInstanceCreated → ResourceReadModel upserted
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task ResourceInstanceCreated_UpsertResourceReadModel()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("avail_resourcecreated@test.com");
        var anonClient = new AvailabilityTestClient(Factory, owner);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var groupResponse = await owner.Client.PostAsJsonAsync("/management-groups", new
        {
            Name = "Availability ResourceCreated Test",
            Description = (string?)null,
            TimeZoneId = "UTC"
        });
        groupResponse.EnsureSuccessStatusCode();
        var groupBody = await groupResponse.Content.ReadFromJsonAsync<CreateGroupResponse>(options);
        Assert.NotNull(groupBody);

        // Wait for group to be visible to resources
        await WaitUntilAsync(async () =>
        {
            var r = await owner.Client.GetAsync($"/resources/types?groupId={groupBody.Id}");
            return r.StatusCode != HttpStatusCode.Forbidden;
        });

        var typeResponse = await owner.Client.PostAsJsonAsync("/resources/types", new
        {
            GroupId = groupBody.Id,
            Name = "Avail Resource Test Schema",
            Description = (string?)null,
            PropertyDefinitions = Array.Empty<object>(),
            BufferMinutes = 0
        });
        typeResponse.EnsureSuccessStatusCode();
        var typeBody = await typeResponse.Content.ReadFromJsonAsync<CreateResourceTypeResponse>(options);
        Assert.NotNull(typeBody);

        // Act — create a resource instance which fires ResourceInstanceCreatedEvent
        var instanceResponse = await owner.Client.PostAsJsonAsync("/resources/instances", new
        {
            ResourceTypeId = typeBody.Id,
            Name = "Test Resource Instance",
            Description = (string?)null,
            PropertyValues = Array.Empty<object>()
        });
        instanceResponse.EnsureSuccessStatusCode();
        var instanceBody = await instanceResponse.Content.ReadFromJsonAsync<CreateInstanceResponse>(options);
        Assert.NotNull(instanceBody);

        // Assert
        var populated = await WaitUntilAsync(() => anonClient.ResourceReadModelExistsAsync(instanceBody.Id));
        Assert.True(populated, "Availability.ResourceReadModel was not populated after ResourceInstanceCreatedEvent.");

        var readModel = await anonClient.GetResourceReadModelFromDbAsync(instanceBody.Id);
        Assert.NotNull(readModel);
        Assert.Equal(typeBody.Id, readModel.SchemaId);
    }

    // -----------------------------------------------------------------------------------------
    // HolidaysRefreshed → PublicHolidayReadModels upserted
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task HolidaysRefreshed_UpsertPublicHolidayReadModels()
    {
        // Arrange — publish a HolidaysRefreshedEvent with 2 holidays for a far-future year
        const int testYear = 2099;
        var holidays = new List<PublicHolidayDto>
        {
            new(new DateOnly(testYear, 1, 1), "Nowy Rok", "New Year's Day"),
            new(new DateOnly(testYear, 12, 25), "Boże Narodzenie", "Christmas Day")
        };

        var @event = new HolidaysRefreshedEvent(testYear, holidays);

        // Set a non-null IContext on the scoped ContextAccessor BEFORE resolving IMessageBroker
        // from the same scope. InMemoryMessageBroker reads IContext via the scoped ContextAccessor;
        // setting it on the root services (Factory.Services) has no effect on child scopes.
        using (var scope = Factory.Services.CreateScope())
        {
            var contextAccessor = scope.ServiceProvider.GetRequiredService<ContextAccessor>();
            contextAccessor.Context = Context.Empty;

            var broker = scope.ServiceProvider.GetRequiredService<IMessageBroker>();
            await broker.PublishAsync(@event);
        }

        // Act — poll the DB until the read models appear (async dispatch)
        var populated = await WaitUntilAsync(async () =>
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();
            var count = await db.PublicHolidayReadModels
                .Where(x => x.Year == testYear)
                .CountAsync();
            return count == 2;
        });

        // Assert
        Assert.True(populated, "PublicHolidayReadModels were not populated after HolidaysRefreshedEvent.");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();
            var entries = await db.PublicHolidayReadModels
                .Where(x => x.Year == testYear)
                .ToListAsync();

            Assert.Equal(2, entries.Count);
        }
    }

    // -----------------------------------------------------------------------------------------
    // Polling helper
    // -----------------------------------------------------------------------------------------

    private static async Task<bool> WaitUntilAsync(
        Func<Task<bool>> condition,
        int maxAttempts = 60,
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

    private record CreateGroupResponse(Guid Id);
    private record CreateResourceTypeResponse(Guid Id);
    private record CreateInstanceResponse(Guid Id);
}
