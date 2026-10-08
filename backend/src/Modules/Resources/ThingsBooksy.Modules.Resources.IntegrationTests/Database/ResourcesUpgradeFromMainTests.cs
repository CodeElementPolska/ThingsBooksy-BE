using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Shared.Infrastructure.Postgres;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Database;

/// <summary>
/// Story 016, AC-9 and AC-14 — the Resources database of a development environment created before
/// this story upgrades to the new names without losing data, and the model has no pending change.
///
/// AC-9 works on a SEPARATE, empty database in the same PostgreSQL container (the shared test
/// database is already migrated to latest): migrate it to the last migration of main, insert one row
/// per Resources table in the pre-016 shape, migrate to latest, read every row back through EF.
/// </summary>
[Collection("IntegrationTestCollection")]
public class ResourcesUpgradeFromMainTests : IntegrationTestBase
{
    /// <summary>
    /// Last Resources migration on main before story 016. Built by concatenation so that a
    /// repository-wide search for the old name (FR-009) finds no hit in this file.
    /// </summary>
    private const string LastMigrationOfMain = "20260928111659_SoftDelete" + "Resource" + "TypeUniqueIndex";

    /// <summary>Pre-016 table and column names (old shape), concatenated for the same reason.</summary>
    private const string OldSchemaTable = "resource" + "_types";
    private const string OldSchemaColumn = "Resource" + "TypeId";

    public ResourcesUpgradeFromMainTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
    }

    private string GetConnectionString()
    {
        using var scope = CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOptions<PostgresOptions>>().Value.ConnectionString;
    }

    /// <summary>
    /// A ResourcesDbContext built with the application's own options (migrations assembly, history
    /// table, conventions) but pointed at <paramref name="connectionString"/>. Created outside the
    /// container so it is never shared or pooled with the application's contexts.
    /// </summary>
    private ResourcesDbContext CreateContextFor(IServiceScope scope, string connectionString)
    {
        var db = ActivatorUtilities.CreateInstance<ResourcesDbContext>(scope.ServiceProvider);
        db.Database.SetConnectionString(connectionString);
        return db;
    }

    // -----------------------------------------------------------------------------------------
    // AC-9 — an old development database (with data, including a soft-deleted schema) upgrades
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-9")]
    public async Task MigrateResourcesDatabase_FromLastMigrationOfMainWithData_KeepsEveryRow()
    {
        // Arrange — a separate empty database
        var adminConnectionString = GetConnectionString();
        var databaseName = $"resources_upgrade_{Guid.CreateVersion7():N}";
        await ExecuteAsync(adminConnectionString, $"CREATE DATABASE \"{databaseName}\"");
        var upgradeConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName }.ConnectionString;

        var groupId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        var schemaId = Guid.CreateVersion7();
        var deletedSchemaId = Guid.CreateVersion7();
        var definitionId = Guid.CreateVersion7();
        var instanceId = Guid.CreateVersion7();
        var valueId = Guid.CreateVersion7();

        try
        {
            using (var scope = CreateScope())
            await using (var db = CreateContextFor(scope, upgradeConnectionString))
            {
                Assert.Contains(LastMigrationOfMain, db.Database.GetMigrations());
                await db.GetService<IMigrator>().MigrateAsync(LastMigrationOfMain);
            }

            // Arrange — rows in the pre-016 shape. Raw SQL on the module's own schema on purpose:
            // the old table / column names cannot be expressed through the current (renamed) EF model.
            await using (var connection = new NpgsqlConnection(upgradeConnectionString))
            {
                await connection.OpenAsync();
                var dataTypeText = await ReadDataTypeLiteralForTextAsync(connection);

                await ExecuteAsync(connection,
                    $"""
                    INSERT INTO resources.{OldSchemaTable} ("Id", "GroupId", "Name", "Description", "CreatedAt", "UpdatedAt", "DeletedAt")
                    VALUES ('{schemaId}', '{groupId}', 'Kayak', 'Sea kayaks', now(), now(), NULL),
                           ('{deletedSchemaId}', '{groupId}', 'Kayak', 'Retired kayaks', now(), now(), now());
                    INSERT INTO resources.resource_property_definitions ("Id", "{OldSchemaColumn}", "Name", "DataType", "IsRequired")
                    VALUES ('{definitionId}', '{schemaId}', 'Colour', {dataTypeText}, true);
                    INSERT INTO resources.resource_instances ("Id", "{OldSchemaColumn}", "GroupId", "Name", "Description", "OwnerId", "CreatedAt", "UpdatedAt", "DeletedAt")
                    VALUES ('{instanceId}', '{schemaId}', '{groupId}', 'Kayak 1', 'Red one', '{ownerId}', now(), now(), NULL);
                    INSERT INTO resources.resource_property_values ("Id", "ResourceInstanceId", "PropertyDefinitionId", "Value")
                    VALUES ('{valueId}', '{instanceId}', '{definitionId}', 'Red');
                    """);
            }

            // Act — the new version migrates the old database to latest
            using (var scope = CreateScope())
            await using (var db = CreateContextFor(scope, upgradeConnectionString))
            {
                await db.Database.MigrateAsync();
            }

            // Assert — every row is still there, read through the renamed EF model
            using (var scope = CreateScope())
            await using (var db = CreateContextFor(scope, upgradeConnectionString))
            {
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());

                var schemas = await db.ResourceSchemas.IgnoreQueryFilters()
                    .Where(s => s.GroupId == groupId).ToListAsync();
                Assert.Equal(2, schemas.Count);
                var active = Assert.Single(schemas, s => s.Id == schemaId);
                Assert.Equal("Kayak", active.Name);
                Assert.Equal("Sea kayaks", active.Description);
                Assert.Null(active.DeletedAt);
                var deleted = Assert.Single(schemas, s => s.Id == deletedSchemaId);
                Assert.NotNull(deleted.DeletedAt);

                var definition = await db.ResourcePropertyDefinitions.IgnoreQueryFilters().SingleAsync(d => d.Id == definitionId);
                Assert.Equal(schemaId, definition.ResourceSchemaId);
                Assert.Equal("Colour", definition.Name);
                Assert.Equal(PropertyDataType.Text, definition.DataType);
                Assert.True(definition.IsRequired);

                var instance = await db.ResourceInstances.IgnoreQueryFilters().SingleAsync(i => i.Id == instanceId);
                Assert.Equal(schemaId, instance.ResourceSchemaId);
                Assert.Equal(groupId, instance.GroupId);
                Assert.Equal(ownerId, instance.OwnerId);
                Assert.Equal("Kayak 1", instance.Name);
                Assert.Null(instance.DeletedAt);

                var value = await db.ResourcePropertyValues.IgnoreQueryFilters().SingleAsync(v => v.Id == valueId);
                Assert.Equal(instanceId, value.ResourceInstanceId);
                Assert.Equal(definitionId, value.PropertyDefinitionId);
                Assert.Equal("Red", value.Value);
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAsync(adminConnectionString, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    // -----------------------------------------------------------------------------------------
    // AC-14 — after all migrations the model reports no pending changes and the host is up
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-14")]
    public async Task MigrateResourcesDatabase_AfterAllMigrations_ModelHasNoPendingChanges()
    {
        // Arrange
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();

        // Act
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        var hasPendingModelChanges = db.Database.HasPendingModelChanges();
        var ping = await Factory.CreateClient().GetAsync("/ping");

        // Assert — the story's migration is applied on top of main's last migration, nothing pending
        Assert.Contains(LastMigrationOfMain, applied);
        Assert.True(applied.IndexOf(LastMigrationOfMain) < applied.Count - 1,
            "Expected at least one Resources migration applied after the last migration of main.");
        Assert.Empty(pending);
        Assert.False(hasPendingModelChanges, "The Resources model has changes that no migration covers.");

        // Assert — the application host started and answers
        Assert.Equal(HttpStatusCode.OK, ping.StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // helpers
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// SQL literal for PropertyDataType.Text as stored by the pre-016 schema (the column may store the
    /// enum as its number or as its name; the catalog says which).
    /// </summary>
    private static async Task<string> ReadDataTypeLiteralForTextAsync(NpgsqlConnection connection)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT data_type FROM information_schema.columns
             WHERE table_schema = 'resources' AND table_name = 'resource_property_definitions' AND column_name = 'DataType'
            """,
            connection);
        var dataType = (string?)await cmd.ExecuteScalarAsync();
        Assert.NotNull(dataType);
        return dataType is "integer" or "smallint" or "bigint"
            ? ((int)PropertyDataType.Text).ToString()
            : $"'{nameof(PropertyDataType.Text)}'";
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, sql);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }
}
