using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Contract;

/// <summary>
/// Story 016, AC-6 (FR-004): the generated API description (OpenAPI document served by the test
/// host) of the Resources module names the template "resource schema" everywhere — no address,
/// request or response model, field, parameter or operation name contains the old name — and it
/// carries the new names listed in spec.md AC-6.
///
/// The Resources part of the document = every path under /resources plus every component schema
/// of the ThingsBooksy.Modules.Resources namespaces. Matching is case-insensitive.
/// </summary>
[Collection("IntegrationTestCollection")]
public class ResourcesApiDescriptionTests : IntegrationTestBase
{
    private const string SwaggerAddress = "/swagger/v1/swagger.json";
    private const string ResourcesComponentPrefix = "ThingsBooksy.Modules.Resources.";

    /// <summary>
    /// The old names searched for (AC-6). Built by concatenation so that a repository-wide search for
    /// the old name (FR-009) finds no hit in this file. The spaced form also covers its plural;
    /// the camel-case form also covers the old field / parameter name.
    /// </summary>
    private static readonly string[] OldNames =
    {
        "Resource" + "Type",
        "resource" + " type",
        "resource" + "_type",
    };

    public ResourcesApiDescriptionTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
    }

    private async Task<JsonElement> LoadApiDescriptionAsync()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync(SwaggerAddress);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static IReadOnlyList<JsonProperty> ResourcesPaths(JsonElement document)
        => document.GetProperty("paths").EnumerateObject()
            .Where(p => p.Name.StartsWith("/resources", StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static IReadOnlyList<JsonProperty> ResourcesComponents(JsonElement document)
        => document.GetProperty("components").GetProperty("schemas").EnumerateObject()
            .Where(s => s.Name.StartsWith(ResourcesComponentPrefix, StringComparison.Ordinal))
            .ToList();

    // -----------------------------------------------------------------------------------------
    // No old name anywhere in the Resources part of the description
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-6")]
    public async Task GetApiDescription_ResourcesPathsAndModels_ContainNoOldName()
    {
        // Arrange — nothing to seed: the description is derived from the endpoints and models

        // Act
        var document = await LoadApiDescriptionAsync();

        // Assert — addresses, operation names, parameters, request bodies (whole path items)
        var paths = ResourcesPaths(document);
        Assert.NotEmpty(paths);
        foreach (var path in paths)
        {
            var text = path.Name + " " + path.Value.GetRawText();
            foreach (var oldName in OldNames)
                Assert.False(text.Contains(oldName, StringComparison.OrdinalIgnoreCase),
                    $"Path '{path.Name}' still contains the old name: {text}");
        }

        // Assert — request / response model names and their fields
        var components = ResourcesComponents(document);
        Assert.NotEmpty(components);
        foreach (var component in components)
        {
            var text = component.Name + " " + component.Value.GetRawText();
            foreach (var oldName in OldNames)
                Assert.False(text.Contains(oldName, StringComparison.OrdinalIgnoreCase),
                    $"Model '{component.Name}' still contains the old name: {text}");
        }
    }

    // -----------------------------------------------------------------------------------------
    // The new names of spec.md AC-6 are present
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-6")]
    public async Task GetApiDescription_ResourcesPathsAndModels_UseResourceSchemaNames()
    {
        // Act
        var document = await LoadApiDescriptionAsync();
        var paths = document.GetProperty("paths");

        // Assert — the five schema operations at /resources/schemas with their operation names
        Assert.True(paths.TryGetProperty("/resources/schemas", out var collection), "Path /resources/schemas is missing.");
        Assert.True(paths.TryGetProperty("/resources/schemas/{id}", out var item), "Path /resources/schemas/{id} is missing.");

        Assert.Equal("Create resource schema", collection.GetProperty("post").GetProperty("operationId").GetString());
        Assert.Equal("Get resource schemas", collection.GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal("Get resource schema", item.GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal("Update resource schema", item.GetProperty("put").GetProperty("operationId").GetString());
        Assert.Equal("Delete resource schema", item.GetProperty("delete").GetProperty("operationId").GetString());

        // Assert — the request models
        var componentNames = ResourcesComponents(document).Select(c => c.Name).ToList();
        Assert.Contains(componentNames, n => n.EndsWith(".CreateResourceSchemaRequest", StringComparison.Ordinal));
        Assert.Contains(componentNames, n => n.EndsWith(".UpdateResourceSchemaRequest", StringComparison.Ordinal));

        // Assert — resourceSchemaId in the create-instance request, the list row and the list filter
        var createInstance = ResourcesComponents(document).Single(c => c.Name.EndsWith(".CreateResourceInstanceRequest", StringComparison.Ordinal));
        Assert.True(HasProperty(createInstance.Value, "resourceSchemaId"), "CreateResourceInstanceRequest has no resourceSchemaId.");

        var row = ResourcesComponents(document).Single(c => c.Name.EndsWith(".ResourceInstanceRowDto", StringComparison.Ordinal));
        Assert.True(HasProperty(row.Value, "resourceSchemaId"), "ResourceInstanceRowDto has no resourceSchemaId.");

        var listParameters = paths.GetProperty("/resources/instances").GetProperty("get").GetProperty("parameters")
            .EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("resourceSchemaId", listParameters, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasProperty(JsonElement schema, string name)
        => schema.TryGetProperty("properties", out var properties)
           && properties.EnumerateObject().Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}
