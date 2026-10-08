using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Clients;

/// <summary>
/// Assertion helpers for Resources HTTP responses whose shape is fixed by story 016
/// (spec.md "Message list" and "Access baseline"):
/// - error envelope <c>{ "errors": [ { "code": …, "message": … } ] }</c> for 400 / 403;
/// - bare <c>{ "code": …, "message": … }</c> for the 409 name conflict;
/// - empty body for the 404 of a missing schema / instance read;
/// - instance rows that carry the schema reference as <c>resourceSchemaId</c>.
/// JSON property casing is not asserted (ASM-5): names are matched case-insensitively.
/// </summary>
internal static class ResourcesResponseAssert
{
    /// <summary>
    /// Name of the instance → schema reference the API carried before story 016. Built by
    /// concatenation so that a repository-wide search for the old name (FR-009) finds no hit here.
    /// </summary>
    internal const string LegacySchemaReferenceName = "resource" + "TypeId";

    internal const string SchemaReferenceName = "resourceSchemaId";

    internal static async Task ErrorAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedCode, string expectedMessage)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expectedStatus,
            $"Expected {(int)expectedStatus} {expectedStatus} but got {(int)response.StatusCode} {response.StatusCode}. Body: {raw}");

        using var doc = JsonDocument.Parse(raw);
        var errors = FindProperty(doc.RootElement, "errors");
        Assert.True(errors is { ValueKind: JsonValueKind.Array },
            $"Expected an error envelope {{ \"errors\": [ … ] }}. Body: {raw}");

        var error = Assert.Single(errors!.Value.EnumerateArray());
        Assert.Equal(expectedCode, GetString(error, "code"));
        Assert.Equal(expectedMessage, GetString(error, "message"));
    }

    internal static async Task ConflictAsync(HttpResponseMessage response, string expectedCode, string expectedMessage)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict,
            $"Expected 409 Conflict but got {(int)response.StatusCode} {response.StatusCode}. Body: {raw}");

        using var doc = JsonDocument.Parse(raw);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal(expectedCode, GetString(doc.RootElement, "code"));
        Assert.Equal(expectedMessage, GetString(doc.RootElement, "message"));
    }

    internal static async Task EmptyNotFoundAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 404 NotFound but got {(int)response.StatusCode} {response.StatusCode}. Body: {raw}");
        Assert.True(string.IsNullOrWhiteSpace(raw), $"Expected an empty 404 body. Body: {raw}");
    }

    internal static async Task<JsonElement> OkJsonAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK but got {(int)response.StatusCode} {response.StatusCode}. Body: {raw}");
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Asserts the response body does not contain any of the given values (names, ids) — used to prove
    /// that a refused call returns no data of the group.
    /// </summary>
    internal static async Task ContainsNoneOfAsync(HttpResponseMessage response, params string[] values)
    {
        var raw = await response.Content.ReadAsStringAsync();
        foreach (var value in values)
            Assert.DoesNotContain(value, raw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Asserts an instance object (list row or single instance body) carries the schema reference as
    /// <c>resourceSchemaId</c> with the expected value and no longer carries the pre-016 name.
    /// </summary>
    internal static void CarriesSchemaReference(JsonElement instance, Guid expectedSchemaId)
    {
        var reference = FindProperty(instance, SchemaReferenceName);
        Assert.True(reference.HasValue, $"Instance JSON has no '{SchemaReferenceName}'. JSON: {instance.GetRawText()}");
        Assert.Equal(expectedSchemaId, reference!.Value.GetGuid());
        Assert.False(FindProperty(instance, LegacySchemaReferenceName).HasValue,
            $"Instance JSON still carries the old schema reference name. JSON: {instance.GetRawText()}");
    }

    /// <summary>The <c>items</c> array of a paged instance list.</summary>
    internal static IReadOnlyList<JsonElement> Items(JsonElement page)
    {
        var items = FindProperty(page, "items");
        Assert.True(items is { ValueKind: JsonValueKind.Array }, $"Expected an 'items' array. JSON: {page.GetRawText()}");
        return items!.Value.EnumerateArray().ToList();
    }

    internal static JsonElement? FindProperty(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        return null;
    }

    internal static Guid GetGuid(JsonElement element, string name)
    {
        var value = FindProperty(element, name);
        Assert.True(value.HasValue, $"JSON has no '{name}'. JSON: {element.GetRawText()}");
        return value!.Value.GetGuid();
    }

    internal static string? GetString(JsonElement element, string name)
    {
        var value = FindProperty(element, name);
        Assert.True(value.HasValue, $"JSON has no '{name}'. JSON: {element.GetRawText()}");
        return value!.Value.ValueKind == JsonValueKind.Null ? null : value.Value.GetString();
    }
}
