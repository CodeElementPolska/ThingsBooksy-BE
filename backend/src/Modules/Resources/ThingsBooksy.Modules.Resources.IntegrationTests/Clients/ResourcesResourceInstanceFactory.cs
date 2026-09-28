using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.Features.CreateResourceInstance;
using ThingsBooksy.Shared.IntegrationTests;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Clients;

/// <summary>
/// Inserts ResourceInstance rows directly into the resources schema through
/// <see cref="ResourceInstance.Create"/> and EF Core — Arrange-phase preconditions that must not
/// depend on POST /resources/instances.
/// </summary>
public sealed class ResourcesResourceInstanceFactory
{
    private readonly ThingsBooksyWebAppFactory _factory;

    public ResourcesResourceInstanceFactory(ThingsBooksyWebAppFactory factory)
        => _factory = factory;

    internal async Task<ResourceInstance> CreateResourceInstanceAsync(
        ResourceType resourceType,
        Guid ownerId,
        string name,
        string? description = null)
    {
        var command = new CreateResourceInstanceCommand(
            resourceType.Id,
            ownerId,
            name,
            description,
            Array.Empty<PropertyValueInput>());

        var instance = ResourceInstance.Create(command, resourceType.GroupId, DateTime.UtcNow);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        db.ResourceInstances.Add(instance);
        await db.SaveChangesAsync();

        return instance;
    }
}
