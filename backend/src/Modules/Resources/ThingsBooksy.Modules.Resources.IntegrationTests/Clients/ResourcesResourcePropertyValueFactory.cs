using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.Features.CreateResourceInstance;
using ThingsBooksy.Shared.IntegrationTests;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Clients;

/// <summary>
/// Inserts ResourcePropertyValue rows directly into the resources schema through
/// <see cref="ResourcePropertyValue.Create"/> and EF Core — Arrange-phase preconditions that must
/// not depend on POST /resources/instances.
/// </summary>
public sealed class ResourcesResourcePropertyValueFactory
{
    private readonly ThingsBooksyWebAppFactory _factory;

    public ResourcesResourcePropertyValueFactory(ThingsBooksyWebAppFactory factory)
        => _factory = factory;

    internal async Task<ResourcePropertyValue> CreateResourcePropertyValueAsync(
        Guid resourceInstanceId,
        Guid propertyDefinitionId,
        string value)
    {
        var propertyValue = ResourcePropertyValue.Create(
            new PropertyValueInput(propertyDefinitionId, value), resourceInstanceId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        db.ResourcePropertyValues.Add(propertyValue);
        await db.SaveChangesAsync();

        return propertyValue;
    }
}
