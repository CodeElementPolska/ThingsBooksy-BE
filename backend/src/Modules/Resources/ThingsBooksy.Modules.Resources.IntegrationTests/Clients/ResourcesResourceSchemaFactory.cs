using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.Domain;
using ThingsBooksy.Modules.Resources.Core.Features.CreateResourceSchema;
using ThingsBooksy.Shared.IntegrationTests;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Clients;

/// <summary>
/// Inserts ResourceSchema rows directly into the resources schema through
/// <see cref="ResourceSchema.Create"/> and EF Core — Arrange-phase preconditions that must not
/// depend on POST /resources/types.
/// </summary>
public sealed class ResourcesResourceSchemaFactory
{
    private readonly ThingsBooksyWebAppFactory _factory;

    public ResourcesResourceSchemaFactory(ThingsBooksyWebAppFactory factory)
        => _factory = factory;

    internal async Task<ResourceSchema> CreateResourceSchemaAsync(
        Guid groupId,
        Guid ownerId,
        string name,
        string? description = null)
    {
        var command = new CreateResourceSchemaCommand(
            groupId,
            ownerId,
            name,
            description,
            Array.Empty<PropertyDefinitionInput>());

        var resourceSchema = ResourceSchema.Create(command, DateTime.UtcNow);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        db.ResourceSchemas.Add(resourceSchema);
        await db.SaveChangesAsync();

        return resourceSchema;
    }
}
