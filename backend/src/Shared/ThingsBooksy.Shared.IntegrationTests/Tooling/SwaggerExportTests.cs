using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ThingsBooksy.Shared.IntegrationTests.Tooling;

/// <summary>
/// Fleet substrate (docs/agent-fleet-v4/workflow.md §5, S2 / decision D-15).
///
/// Exports the OpenAPI document and the module list of the BUILT backend into
/// <c>generated/</c> at the repository root, so that fleet scripts (capability-map,
/// contract-validate, contract-diff) work from a deterministic artifact instead of a
/// hand-written contract or a manually started process.
///
/// Run only this test:  dotnet test backend/src/Shared/ThingsBooksy.Shared.IntegrationTests --filter Category=Tooling
/// Override output dir: FLEET_GENERATED_DIR=&lt;absolute path&gt;
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
[Trait("Category", "Tooling")]
public class SwaggerExportTests
{
    private readonly ThingsBooksyWebAppFactory _factory;

    public SwaggerExportTests(ThingsBooksyWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExportSwagger_FromBuiltBackend_WritesGeneratedArtifacts()
    {
        var client = _factory.CreateClient();

        var swaggerResponse = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swaggerResponse.StatusCode);
        var swagger = await swaggerResponse.Content.ReadAsStringAsync();
        using (var doc = JsonDocument.Parse(swagger))
        {
            Assert.True(doc.RootElement.TryGetProperty("paths", out var paths), "swagger has no 'paths'");
            Assert.True(paths.EnumerateObject().MoveNext(), "swagger has zero paths");
        }

        var modulesResponse = await client.GetAsync("/modules");
        Assert.Equal(HttpStatusCode.OK, modulesResponse.StatusCode);
        var modules = await modulesResponse.Content.ReadAsStringAsync();

        var outDir = ResolveGeneratedDir();
        Directory.CreateDirectory(outDir);
        await File.WriteAllTextAsync(Path.Combine(outDir, "swagger.base.json"), Pretty(swagger));
        await File.WriteAllTextAsync(Path.Combine(outDir, "modules.json"), Pretty(modules));
    }

    private static string ResolveGeneratedDir()
    {
        var env = Environment.GetEnvironmentVariable("FLEET_GENERATED_DIR");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException("Repository root (CLAUDE.md) not found above " + AppContext.BaseDirectory);

        return Path.Combine(dir.FullName, "generated");
    }

    private static string Pretty(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
    }
}
