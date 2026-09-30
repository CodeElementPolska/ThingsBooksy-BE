using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.Features.CreateResourceType;
using ThingsBooksy.Shared.IntegrationTests;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Clients;

/// <summary>
/// Inserts ResourceType rows directly into the resources schema through
/// <see cref="ResourceType.Create"/> and EF Core — Arrange-phase preconditions that must not
/// depend on POST /resources/types.
/// </summary>
public sealed class ResourcesResourceTypeFactory
{
    private readonly ThingsBooksyWebAppFactory _factory;

    public ResourcesResourceTypeFactory(ThingsBooksyWebAppFactory factory)
        => _factory = factory;

    internal async Task<ResourceType> CreateResourceTypeAsync(
        Guid groupId,
        Guid ownerId,
        string name,
        string? description = null)
    {
        var command = new CreateResourceTypeCommand(
            groupId,
            ownerId,
            name,
            description,
            Array.Empty<PropertyDefinitionInput>());

        var resourceType = ResourceType.Create(command, DateTime.UtcNow);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        db.ResourceTypes.Add(resourceType);
        await db.SaveChangesAsync();

        return resourceType;
    }
}
