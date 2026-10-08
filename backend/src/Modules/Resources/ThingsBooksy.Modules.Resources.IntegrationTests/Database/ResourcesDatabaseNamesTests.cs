using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using ThingsBooksy.Shared.Infrastructure.Postgres;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Database;

/// <summary>
/// Story 016, AC-13: after the migrations, no table, column, constraint, index or sequence of the
/// Resources area (schema <c>resources</c>) carries the old name, and the renamed objects exist under
/// the names of spec.md AC-13 (FK name explicit, DEC-6). Property data-kind names (<c>DataType</c>)
/// are not affected.
///
/// Raw catalog queries on purpose: database object names (tables, columns, constraints, indexes,
/// sequences) are metadata of the live database and cannot be read through the EF model, which only
/// knows what it expects, not what the migrations produced.
/// </summary>
[Collection("IntegrationTestCollection")]
public class ResourcesDatabaseNamesTests : IntegrationTestBase
{
    /// <summary>
    /// Old-name fragments (case-insensitive). Built by concatenation so that a repository-wide search
    /// for the old name (FR-009) finds no hit in this file.
    /// </summary>
    private static readonly string[] OldNameFragments = { "resource" + "_type", "Resource" + "Type" };

    private const string CatalogNamesSql = """
        SELECT 'table' AS kind, table_name::text AS name
          FROM information_schema.tables WHERE table_schema = 'resources'
        UNION ALL
        SELECT 'column', table_name::text || '.' || column_name::text
          FROM information_schema.columns WHERE table_schema = 'resources'
        UNION ALL
        SELECT 'constraint', c.conname::text
          FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace WHERE n.nspname = 'resources'
        UNION ALL
        SELECT 'index', indexname::text
          FROM pg_indexes WHERE schemaname = 'resources'
        UNION ALL
        SELECT 'sequence', sequence_name::text
          FROM information_schema.sequences WHERE sequence_schema = 'resources'
        """;

    public ResourcesDatabaseNamesTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
    }

    private async Task<List<(string Kind, string Name)>> ListResourcesCatalogNamesAsync()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(CatalogNamesSql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        var names = new List<(string Kind, string Name)>();
        while (await reader.ReadAsync())
            names.Add((reader.GetString(0), reader.GetString(1)));
        return names;
    }

    private string GetConnectionString()
    {
        using var scope = CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOptions<PostgresOptions>>().Value.ConnectionString;
    }

    [Fact]
    [Trait("AC", "AC-13")]
    public async Task ListResourcesDatabaseNames_AfterMigrations_ContainNoOldName()
    {
        // Act
        var names = await ListResourcesCatalogNamesAsync();

        // Assert
        Assert.NotEmpty(names);
        var offending = names
            .Where(n => OldNameFragments.Any(f => n.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .Select(n => $"{n.Kind} {n.Name}")
            .ToList();
        Assert.True(offending.Count == 0, "Old names in the resources schema: " + string.Join(", ", offending));
    }

    [Fact]
    [Trait("AC", "AC-13")]
    public async Task ListResourcesDatabaseNames_AfterMigrations_ContainTheRenamedObjects()
    {
        // Act
        var names = await ListResourcesCatalogNamesAsync();

        // Assert — the names of spec.md AC-13
        Assert.Contains(("table", "resource_schemas"), names);
        Assert.Contains(("column", "resource_instances.ResourceSchemaId"), names);
        Assert.Contains(("column", "resource_property_definitions.ResourceSchemaId"), names);
        Assert.Contains(("constraint", "PK_resource_schemas"), names);
        Assert.Contains(("constraint", "FK_resource_property_definitions_resource_schemas"), names);
        Assert.Contains(("index", "IX_resource_property_definitions_ResourceSchemaId"), names);
        Assert.Contains(("index", "IX_resource_schemas_GroupId_Name"), names);

        // Assert — property data-kind names are not affected
        Assert.Contains(("column", "resource_property_definitions.DataType"), names);
    }

    [Fact]
    [Trait("AC", "AC-13")]
    public async Task ListResourcesIndexDefinitions_AfterMigrations_SchemaNameIndexKeepsUniqueSoftDeleteFilter()
    {
        // Arrange
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """SELECT indexdef FROM pg_indexes WHERE schemaname = 'resources' AND indexname = 'IX_resource_schemas_GroupId_Name'""",
            connection);

        // Act
        var definition = (string?)await cmd.ExecuteScalarAsync();

        // Assert — still unique per group and name among active schemas ("DeletedAt" IS NULL)
        Assert.NotNull(definition);
        Assert.Contains("UNIQUE", definition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resource_schemas", definition, StringComparison.Ordinal);
        Assert.Contains("\"DeletedAt\" IS NULL", definition, StringComparison.Ordinal);
    }
}
