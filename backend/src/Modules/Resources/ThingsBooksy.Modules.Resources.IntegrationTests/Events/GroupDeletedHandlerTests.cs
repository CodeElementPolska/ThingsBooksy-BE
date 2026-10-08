using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.Abstractions.Events;
using ThingsBooksy.Shared.Abstractions.Events.ManagementGroups;
using ThingsBooksy.Shared.Infrastructure.Contexts;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Events;

/// <summary>
/// Integration tests for GroupDeletedHandler (T071).
///
/// Verifies that the handler is idempotent: publishing a GroupDeleted event a second time
/// for a group that was already cleaned up must not throw and must not alter row counts.
///
/// The handler is invoked directly via DI (IEventHandler&lt;GroupDeleted&gt;) because the
/// ManagementGroups HTTP endpoint cannot be called twice for the same group — the group
/// is already gone after the first delete. Direct DI invocation bypasses the async
/// background channel and executes synchronously within the test, making the assertion
/// deterministic without polling.
/// </summary>
[Collection("IntegrationTestCollection")]
public class GroupDeletedHandlerTests : IntegrationTestBase
{
    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;

    public GroupDeletedHandlerTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // T071 — Redelivered GroupDeleted event is a no-op (idempotency contract)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task GroupDeletedHandler_RedeliveredEvent_IsNoOp()
    {
        // Arrange — seed via HTTP to exercise the full creation pipeline
        var owner = await _users.CreateUserAsync("gdh_idempotent_owner@test.com");
        var client = new ResourcesTestClient(Factory, owner);

        // Create a group through ManagementGroups HTTP endpoint so the GroupReadModel is populated
        // in the Resources schema via the GroupCreatedHandler (async). Poll until available.
        var groupId = await client.CreateGroupAndGetIdAsync("GroupDeletedHandler Idempotency Group");
        var groupModelReady = await WaitUntilAsync(() => client.GroupReadModelExistsAsync(groupId));
        Assert.True(groupModelReady, "Pre-condition: GroupReadModel was not populated after group creation.");

        // Create 2 resource schemas and 5 instances through the Resources HTTP API
        var typeId1 = await client.CreateResourceSchemaAndGetIdAsync(groupId, "HandlerType1");
        var typeId2 = await client.CreateResourceSchemaAndGetIdAsync(groupId, "HandlerType2");

        await client.CreateResourceInstanceAndGetIdAsync(typeId1, "HandlerInst1");
        await client.CreateResourceInstanceAndGetIdAsync(typeId1, "HandlerInst2");
        await client.CreateResourceInstanceAndGetIdAsync(typeId1, "HandlerInst3");
        await client.CreateResourceInstanceAndGetIdAsync(typeId2, "HandlerInst4");
        await client.CreateResourceInstanceAndGetIdAsync(typeId2, "HandlerInst5");

        var groupDeletedEvent = new GroupDeleted(groupId);

        // Act — first invocation: clean up types, soft-delete instances, remove GroupReadModel
        await InvokeHandlerAsync(groupDeletedEvent);

        // Assert after first invocation
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();

            var groupReadModel = await db.GroupReadModels
                .FirstOrDefaultAsync(g => g.Id == groupId);
            Assert.Null(groupReadModel);

            var types = await db.ResourceSchemas
                .IgnoreQueryFilters()
                .Where(t => t.GroupId == groupId)
                .ToListAsync();
            Assert.Empty(types);

            var instances = await db.ResourceInstances
                .IgnoreQueryFilters()
                .Where(i => i.GroupId == groupId)
                .ToListAsync();
            Assert.Equal(5, instances.Count);
            Assert.All(instances, i => Assert.NotNull(i.DeletedAt));
        }

        // Capture row counts before second invocation to confirm idempotency
        int instanceCountBefore;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();
            instanceCountBefore = await db.ResourceInstances
                .IgnoreQueryFilters()
                .CountAsync(i => i.GroupId == groupId);
        }

        // Act — second invocation with the same event (redelivery simulation)
        // Must not throw — all three operations are no-ops when data is already gone
        var exception = await Record.ExceptionAsync(() => InvokeHandlerAsync(groupDeletedEvent));
        Assert.Null(exception);

        // Assert — row counts are unchanged (handler was a true no-op)
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();

            var instanceCountAfter = await db.ResourceInstances
                .IgnoreQueryFilters()
                .CountAsync(i => i.GroupId == groupId);
            Assert.Equal(instanceCountBefore, instanceCountAfter);

            var typeCount = await db.ResourceSchemas
                .IgnoreQueryFilters()
                .CountAsync(t => t.GroupId == groupId);
            Assert.Equal(0, typeCount);

            var groupReadModel = await db.GroupReadModels
                .FirstOrDefaultAsync(g => g.Id == groupId);
            Assert.Null(groupReadModel);
        }
    }

    // -----------------------------------------------------------------------------------------
    // DI helper — resolves a fresh IEventHandler<GroupDeleted> scope and invokes the handler
    // -----------------------------------------------------------------------------------------

    private async Task InvokeHandlerAsync(GroupDeleted @event)
    {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ContextAccessor>().Context = Context.Empty;
        var handler = scope.ServiceProvider.GetRequiredService<IEventHandler<GroupDeleted>>();
        await handler.HandleAsync(@event);
    }

    // -----------------------------------------------------------------------------------------
    // Polling helper — retries the condition every 100 ms for up to 6 seconds
    // -----------------------------------------------------------------------------------------

    private static async Task<bool> WaitUntilAsync(
        Func<Task<bool>> condition,
        int maxAttempts = 60,
        int intervalMs = 100)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            if (await condition())
                return true;

            await Task.Delay(intervalMs);
        }

        return false;
    }
}
