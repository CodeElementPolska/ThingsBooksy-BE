using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.Infrastructure.Postgres;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceTypes;

/// <summary>
/// Integration tests verifying that ResourceSchemaCreatedEvent and ResourceInstanceCreatedEvent
/// are published correctly when resource types and instances are created (T021).
///
/// The outbox is disabled in the test environment (appsettings.json "outbox": {"enabled": false}).
/// Events flow synchronously through the in-memory message broker → async dispatcher channel →
/// Availability module event handlers → AvailabilityDbContext.
///
/// Event emission is verified by observing the downstream side-effects in the Availability
/// module's read-model tables (availability.schema_read_models and availability.resource_read_models)
/// via raw SQL polling with WaitUntilAsync, consistent with the pattern used in
/// GroupReadModelEventHandlerTests for incoming events.
///
/// The BufferMinutes persistence test creates a type via HTTP and reads back the DB row
/// directly to confirm the column value is stored.
/// </summary>
[Collection("IntegrationTestCollection")]
public class CreateResourceTypeEventTests : IntegrationTestBase
{
    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;

    public CreateResourceTypeEventTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // T021a — ResourceSchemaCreatedEvent is published after resource type creation
    // Verification: the Availability module's schema_read_models table is populated with
    // a row matching the newly created resource type ID, proving the event was emitted and
    // handled by the Availability ResourceSchemaCreatedHandler.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceType_Returns201_AndEmitsResourceSchemaCreatedEvent()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("creatert_event_schema_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        // Act
        var response = await client.CreateResourceTypeAsync(group.Id, "EventDeskSchema");

        // Assert — HTTP 201
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var rawBody = await response.Content.ReadAsStringAsync();
        var body = System.Text.Json.JsonSerializer.Deserialize<CreateResourceTypeResponse>(rawBody, options);
        Assert.NotNull(body);

        // Assert — Availability.SchemaReadModel populated, proving ResourceSchemaCreatedEvent was emitted
        var schemaId = body.Id;
        var populated = await WaitUntilAsync(() => AvailabilitySchemaReadModelExistsAsync(schemaId));
        Assert.True(populated,
            $"Availability.SchemaReadModel was not populated after resource type creation — " +
            $"ResourceSchemaCreatedEvent was not published or handled (SchemaId: {schemaId}).");
    }

    // -----------------------------------------------------------------------------------------
    // T021b — BufferMinutes is persisted correctly when provided
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceType_WithBufferMinutes_PersistsCorrectly()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("creatert_buffer_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        // Act — pass BufferMinutes = 15 via HTTP
        var response = await client.CreateResourceTypeWithBufferAsync(group.Id, "BufferedDesk", bufferMinutes: 15);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var rawBody = await response.Content.ReadAsStringAsync();
        var body = System.Text.Json.JsonSerializer.Deserialize<CreateResourceTypeResponse>(rawBody, options);
        Assert.NotNull(body);

        // Assert — DB row has BufferMinutes = 15
        var resourceType = await client.GetResourceTypeFromDbAsync(body.Id);
        Assert.NotNull(resourceType);
        Assert.Equal(15, resourceType.BufferMinutes);
    }

    // -----------------------------------------------------------------------------------------
    // T021c — BufferMinutes defaults to 0 when not provided
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceType_WithoutBufferMinutes_DefaultsToZero()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("creatert_buffer_default_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        // Act — omit BufferMinutes (use the existing helper that sends no BufferMinutes field)
        var typeId = await client.CreateResourceTypeAndGetIdAsync(group.Id, "DefaultBufferDesk");

        // Assert — DB row has BufferMinutes = 0
        var resourceType = await client.GetResourceTypeFromDbAsync(typeId);
        Assert.NotNull(resourceType);
        Assert.Equal(0, resourceType.BufferMinutes);
    }

    // -----------------------------------------------------------------------------------------
    // T021d — ResourceInstanceCreatedEvent is published after resource instance creation
    // Verification: the Availability module's resource_read_models table is populated with
    // a row matching the newly created resource instance ID, proving the event was emitted
    // and handled by the Availability ResourceInstanceCreatedHandler.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResourceInstance_Returns201_AndEmitsResourceInstanceCreatedEvent()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("createri_event_instance_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);

        // Pre-populate Availability.SchemaReadModel so the Availability handler can link the
        // resource instance to its schema (required for foreign key in some configurations).
        var typeId = await client.CreateResourceTypeAndGetIdAsync(group.Id, "EventDeskInstance");

        // Wait for the schema read model to be populated before creating the instance
        var schemaPopulated = await WaitUntilAsync(() => AvailabilitySchemaReadModelExistsAsync(typeId));
        Assert.True(schemaPopulated,
            "Pre-condition failed: Availability.SchemaReadModel not populated after resource type creation.");

        // Act
        var response = await client.CreateResourceInstanceAsync(typeId, "Desk Event A");

        // Assert — HTTP 201
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var rawBody = await response.Content.ReadAsStringAsync();
        var body = System.Text.Json.JsonSerializer.Deserialize<CreateResourceInstanceResponse>(rawBody, options);
        Assert.NotNull(body);

        // Assert — Availability.ResourceReadModel populated, proving ResourceInstanceCreatedEvent was emitted
        var resourceId = body.Id;
        var populated = await WaitUntilAsync(() => AvailabilityResourceReadModelExistsAsync(resourceId));
        Assert.True(populated,
            $"Availability.ResourceReadModel was not populated after resource instance creation — " +
            $"ResourceInstanceCreatedEvent was not published or handled (ResourceId: {resourceId}).");
    }

    // -----------------------------------------------------------------------------------------
    // Raw SQL helpers — query availability schema read-models without importing
    // AvailabilityDbContext (cross-module type import is forbidden by architecture rules).
    // -----------------------------------------------------------------------------------------

    private async Task<bool> AvailabilitySchemaReadModelExistsAsync(Guid schemaId)
    {
        var connectionString = GetConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """SELECT COUNT(1) FROM availability.schema_read_models WHERE "Id" = @id""",
            connection);
        cmd.Parameters.AddWithValue("id", schemaId);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return count > 0;
    }

    private async Task<bool> AvailabilityResourceReadModelExistsAsync(Guid resourceId)
    {
        var connectionString = GetConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """SELECT COUNT(1) FROM availability.resource_read_models WHERE "Id" = @id""",
            connection);
        cmd.Parameters.AddWithValue("id", resourceId);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return count > 0;
    }

    private string GetConnectionString()
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<IOptions<PostgresOptions>>().Value.ConnectionString;
    }

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
}
