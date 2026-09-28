using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.Features.CreateResourceType;
using ThingsBooksy.Shared.IntegrationTests;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Clients;

/// <summary>
/// Inserts ResourcePropertyDefinition rows directly into the resources schema through
/// <see cref="ResourcePropertyDefinition.Create"/> and EF Core — Arrange-phase preconditions that must
/// not depend on POST /resources/types.
/// </summary>
public sealed class ResourcesResourcePropertyDefinitionFactory
{
    private readonly ThingsBooksyWebAppFactory _factory;

    public ResourcesResourcePropertyDefinitionFactory(ThingsBooksyWebAppFactory factory)
        => _factory = factory;

    internal async Task<ResourcePropertyDefinition> CreateResourcePropertyDefinitionAsync(
        Guid resourceTypeId,
        string name,
        PropertyDataType dataType,
        bool isRequired = false)
    {
        var definition = ResourcePropertyDefinition.Create(
            new PropertyDefinitionInput(name, dataType, isRequired), resourceTypeId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        db.ResourcePropertyDefinitions.Add(definition);
        await db.SaveChangesAsync();

        return definition;
    }
}
