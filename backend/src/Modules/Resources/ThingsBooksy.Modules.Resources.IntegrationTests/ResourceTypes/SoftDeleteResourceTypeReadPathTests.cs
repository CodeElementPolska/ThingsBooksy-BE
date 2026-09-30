using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceTypes;

/// <summary>
/// Second-pass (sighted) tests for story 015, AC-10 (DEC-6): after a schema is soft-deleted its
/// property definitions are kept (ASM-15), so GET /resources/instances?groupId=&amp;includeDeleted=true
/// returns the soft-deleted instances with property values that carry the definition's real
/// Name and DataType (not empty strings).
///
/// Arrange = EF seeding (factories), Act = HTTP DELETE then HTTP GET,
/// Assert = response body + DB re-read through the TestClient (IgnoreQueryFilters).
/// </summary>
[Collection("IntegrationTestCollection")]
public class SoftDeleteResourceTypeReadPathTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Local response records — test-only, no import of Core DTOs
    private record PropertyValueDto(Guid PropertyDefinitionId, string PropertyName, string DataType, string Value);
    private record RowDto(Guid Id, Guid ResourceTypeId, Guid GroupId, string Name, DateTime? DeletedAt, List<PropertyValueDto> PropertyValues);
    private record PagedResponse(List<RowDto> Items, Guid? NextCursor);

    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;
    private readonly ResourcesResourceTypeFactory _types;
    private readonly ResourcesResourceInstanceFactory _instances;
    private readonly ResourcesResourcePropertyDefinitionFactory _propertyDefinitions;
    private readonly ResourcesResourcePropertyValueFactory _propertyValues;

    public SoftDeleteResourceTypeReadPathTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
        _types = new ResourcesResourceTypeFactory(factory);
        _instances = new ResourcesResourceInstanceFactory(factory);
        _propertyDefinitions = new ResourcesResourcePropertyDefinitionFactory(factory);
        _propertyValues = new ResourcesResourcePropertyValueFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // AC-10 — GET /resources/instances?groupId=&includeDeleted=true after the schema is soft-deleted
    //         returns the property values with the definition's real Name and DataType
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "AC-10")]
    public async Task GetResourceInstances_IncludeDeletedForSoftDeletedType_ReturnsPropertyValuesWithDefinitionNames()
    {
        // Arrange — schema with two property definitions and one instance carrying a value for each
        var owner = await _users.CreateUserAsync("softdelrt_readpath_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var resourceType = await _types.CreateResourceTypeAsync(group.Id, owner.UserId, "Kayak");
        var seatsDef = await _propertyDefinitions.CreateResourcePropertyDefinitionAsync(resourceType.Id, "Seats", PropertyDataType.Number);
        var colourDef = await _propertyDefinitions.CreateResourcePropertyDefinitionAsync(resourceType.Id, "Colour", PropertyDataType.Text);
        var instance = await _instances.CreateResourceInstanceAsync(resourceType, owner.UserId, "Kayak 1");
        await _propertyValues.CreateResourcePropertyValueAsync(instance.Id, seatsDef.Id, "2");
        await _propertyValues.CreateResourcePropertyValueAsync(instance.Id, colourDef.Id, "Red");
        var client = new ResourcesTestClient(Factory, owner);

        var deleteResponse = await client.DeleteResourceTypeAsync(resourceType.Id);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Act
        var response = await client.GetResourceInstancesAsync(groupId: group.Id, includeDeleted: true);

        // Assert — 200 OK and the soft-deleted instance is listed
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse>(JsonOptions);
        Assert.NotNull(body);
        var row = Assert.Single(body.Items, i => i.Id == instance.Id);
        Assert.Equal(resourceType.Id, row.ResourceTypeId);
        Assert.NotNull(row.DeletedAt);

        // Assert — each property value carries the definition's real Name and DataType
        Assert.Equal(2, row.PropertyValues.Count);

        var seats = Assert.Single(row.PropertyValues, pv => pv.PropertyDefinitionId == seatsDef.Id);
        Assert.Equal("Seats", seats.PropertyName);
        Assert.Equal(nameof(PropertyDataType.Number), seats.DataType);
        Assert.Equal("2", seats.Value);

        var colour = Assert.Single(row.PropertyValues, pv => pv.PropertyDefinitionId == colourDef.Id);
        Assert.Equal("Colour", colour.PropertyName);
        Assert.Equal(nameof(PropertyDataType.Text), colour.DataType);
        Assert.Equal("Red", colour.Value);

        Assert.All(row.PropertyValues, pv =>
        {
            Assert.False(string.IsNullOrEmpty(pv.PropertyName));
            Assert.False(string.IsNullOrEmpty(pv.DataType));
        });

        // Assert — DB: the schema is soft-deleted and its property definitions are kept (ASM-15)
        var typeRow = await client.GetResourceTypeFromDbIgnoringFiltersAsync(resourceType.Id);
        Assert.NotNull(typeRow);
        Assert.NotNull(typeRow.DeletedAt);

        var definitionIds = (await client.GetResourcePropertyDefinitionsFromDbAsync(resourceType.Id))
            .Select(d => d.Id)
            .ToList();
        Assert.Equal(2, definitionIds.Count);
        Assert.Contains(seatsDef.Id, definitionIds);
        Assert.Contains(colourDef.Id, definitionIds);
    }
}
