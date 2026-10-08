using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.Core.ReadModels;
using ThingsBooksy.Shared.Abstractions.Events.ManagementGroups;
using ThingsBooksy.Shared.IntegrationTests;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Clients;

/// <summary>
/// Inserts GroupReadModel rows directly into the resources schema.
///
/// Use this factory as test precondition for POST /resources/schemas tests.
/// Bypasses the ManagementGroups event pipeline so tests do not depend on
/// async event propagation timing.
/// </summary>
public sealed class ResourcesGroupReadModelFactory
{
    private readonly ThingsBooksyWebAppFactory _factory;

    public ResourcesGroupReadModelFactory(ThingsBooksyWebAppFactory factory)
    {
        _factory = factory;
    }

    internal async Task<GroupReadModel> CreateGroupReadModelAsync(Guid ownerId)
    {
        var groupId = Guid.CreateVersion7();
        var readModel = GroupReadModel.Upsert(new GroupCreated(groupId, ownerId));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        db.GroupReadModels.Add(readModel);
        await db.SaveChangesAsync();

        return readModel;
    }

    internal async Task AddGroupMemberAsync(Guid groupId, Guid userId)
    {
        var member = GroupMemberReadModel.Upsert(new GroupMemberAdded(groupId, userId));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
        db.GroupMemberReadModels.Add(member);
        await db.SaveChangesAsync();
    }
}
