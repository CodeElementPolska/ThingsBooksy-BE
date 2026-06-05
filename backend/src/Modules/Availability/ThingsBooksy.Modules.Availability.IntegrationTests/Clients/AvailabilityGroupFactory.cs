using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Availability.Core.DAL;
using ThingsBooksy.Modules.Availability.Core.ReadModels;
using ThingsBooksy.Shared.Abstractions.Events.ManagementGroups;
using ThingsBooksy.Shared.Abstractions.Events.Resources;
using ThingsBooksy.Shared.IntegrationTests;

namespace ThingsBooksy.Modules.Availability.IntegrationTests.Clients;

/// <summary>
/// Inserts read-model rows directly into the availability schema as test preconditions,
/// bypassing async event propagation timing.
/// </summary>
public sealed class AvailabilityGroupFactory
{
    private readonly ThingsBooksyWebAppFactory _factory;

    public AvailabilityGroupFactory(ThingsBooksyWebAppFactory factory)
        => _factory = factory;

    internal async Task<GroupReadModel> CreateGroupReadModelAsync(Guid ownerId, string timeZoneId = "UTC")
    {
        var groupId = Guid.CreateVersion7();
        var readModel = GroupReadModel.Upsert(new GroupCreated(groupId, ownerId, timeZoneId));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();
        db.GroupReadModels.Add(readModel);
        await db.SaveChangesAsync();

        return readModel;
    }

    internal async Task<SchemaReadModel> CreateSchemaReadModelAsync(Guid groupId, int defaultBufferMinutes = 0)
    {
        var schemaId = Guid.CreateVersion7();
        var readModel = SchemaReadModel.Upsert(new ResourceSchemaCreatedEvent(schemaId, groupId, defaultBufferMinutes));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();
        db.SchemaReadModels.Add(readModel);
        await db.SaveChangesAsync();

        return readModel;
    }

    internal async Task<ResourceReadModel> CreateResourceReadModelAsync(Guid schemaId)
    {
        var resourceId = Guid.CreateVersion7();
        var readModel = ResourceReadModel.Upsert(new ResourceInstanceCreatedEvent(resourceId, schemaId));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();
        db.ResourceReadModels.Add(readModel);
        await db.SaveChangesAsync();

        return readModel;
    }
}
