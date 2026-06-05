using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;
using Xunit;

namespace ThingsBooksy.Shared.IntegrationTests;

public class ThingsBooksyWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private Respawner _respawner = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(AppDomain.CurrentDomain.BaseDirectory);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["postgres:connectionString"] = _postgres.GetConnectionString(),
                ["logger:console:enabled"] = "false",
                ["logger:file:enabled"] = "false",
                ["managementgroups:module:enabled"] = "true",
                ["resources:module:enabled"] = "true",
                ["calendar:module:enabled"] = "true",
                ["availability:module:enabled"] = "true",
            });
        });
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();
        _ = CreateClient();
        await ApplyMigrationsAsync();
        await InitializeRespawnerAsync();
    }

    private async Task ApplyMigrationsAsync()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;

        var dbContextTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsSubclassOf(typeof(DbContext)) && !t.IsAbstract);

        foreach (var dbContextType in dbContextTypes)
        {
            if (sp.GetService(dbContextType) is DbContext context)
                await context.Database.MigrateAsync();
        }
    }

    private async Task InitializeRespawnerAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["users", "management_groups", "resources", "calendar", "availability"],
            TablesToIgnore = [new Table("users", "Roles")],
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
