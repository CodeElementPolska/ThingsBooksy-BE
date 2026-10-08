using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Resources.Core.DAL;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.Abstractions.Events.Resources;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Events;

/// <summary>
/// Second-pass (sighted) tests for story 015: deleting an already soft-deleted schema or instance
/// goes through the global query filter, returns the existing "not found" domain error (HTTP 400)
/// and publishes no lifecycle event (spec edge case, ASM-19). Not covered by an AC — tagged
/// UNSPECIFIED for the owner's decision at the gate.
///
/// Arrange = EF seeding (factories + soft delete through the domain method), Act = HTTP,
/// Assert = DB re-read (IgnoreQueryFilters) plus the recorded messages.
/// </summary>
[Collection("IntegrationTestCollection")]
public class ResourceLifecycleEventNoPublishTests : IntegrationTestBase
{
    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;
    private readonly ResourcesResourceSchemaFactory _types;
    private readonly ResourcesResourceInstanceFactory _instances;

    public ResourceLifecycleEventNoPublishTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
        _types = new ResourcesResourceSchemaFactory(factory);
        _instances = new ResourcesResourceInstanceFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // UNSPECIFIED (ASM-19) — DELETE /resources/schemas/{id} on an already soft-deleted schema
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "UNSPECIFIED")]
    public async Task DeleteResourceSchema_AlreadyDeleted_Returns400AndPublishesNothing()
    {
        // Arrange — schema with one instance, both already soft-deleted in the DB
        var owner = await _users.CreateUserAsync("evt_nopub_deletert_deleted_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var resourceSchema = await _types.CreateResourceSchemaAsync(group.Id, owner.UserId, "Canoe");
        var instance = await _instances.CreateResourceInstanceAsync(resourceSchema, owner.UserId, "Canoe 1");
        await SoftDeleteResourceSchemaAndInstancesInDbAsync(resourceSchema.Id);

        var client = new ResourcesTestClient(Factory, owner);
        var typeBefore = await client.GetResourceSchemaFromDbAsync(resourceSchema.Id);
        var instanceBefore = await client.GetResourceInstanceFromDbAsync(instance.Id);
        Assert.NotNull(typeBefore?.DeletedAt);
        Assert.NotNull(instanceBefore?.DeletedAt);
        Factory.PublishedMessages.Clear();

        // Act
        var response = await client.DeleteResourceSchemaAsync(resourceSchema.Id);

        // Assert — the query filter hides the row → existing "not found" domain error → 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert — the rows are untouched: DeletedAt / UpdatedAt keep the first deletion's values
        var typeAfter = await client.GetResourceSchemaFromDbAsync(resourceSchema.Id);
        Assert.NotNull(typeAfter);
        Assert.Equal(typeBefore!.DeletedAt, typeAfter.DeletedAt);
        Assert.Equal(typeBefore.UpdatedAt, typeAfter.UpdatedAt);

        var instanceAfter = await client.GetResourceInstanceFromDbAsync(instance.Id);
        Assert.NotNull(instanceAfter);
        Assert.Equal(instanceBefore!.DeletedAt, instanceAfter.DeletedAt);
        Assert.Equal(instanceBefore.UpdatedAt, instanceAfter.UpdatedAt);

        // Assert — no lifecycle event is published for the schema or its instance
        Assert.Empty(Factory.PublishedMessages.OfType<ResourceSchemaDeletedEvent>());
        Assert.Empty(Factory.PublishedMessages.OfType<ResourceInstanceDeletedEvent>());
        Assert.DoesNotContain(
            Factory.PublishedMessages.Published,
            m => m.GetType().Namespace == typeof(ResourceSchemaDeletedEvent).Namespace);
    }

    // -----------------------------------------------------------------------------------------
    // UNSPECIFIED (ASM-19) — DELETE /resources/instances/{id} on an already soft-deleted instance
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "UNSPECIFIED")]
    public async Task DeleteResourceInstance_AlreadyDeleted_Returns400AndPublishesNothing()
    {
        // Arrange — active schema, instance already soft-deleted in the DB
        var owner = await _users.CreateUserAsync("evt_nopub_deleteri_deleted_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var resourceSchema = await _types.CreateResourceSchemaAsync(group.Id, owner.UserId, "Scooter");
        var instance = await _instances.CreateResourceInstanceAsync(resourceSchema, owner.UserId, "Scooter 1");
        await SoftDeleteResourceInstanceInDbAsync(instance.Id);

        var client = new ResourcesTestClient(Factory, owner);
        var instanceBefore = await client.GetResourceInstanceFromDbAsync(instance.Id);
        Assert.NotNull(instanceBefore?.DeletedAt);
        Factory.PublishedMessages.Clear();

        // Act
        var response = await client.DeleteResourceInstanceAsync(instance.Id);

        // Assert — the query filter hides the row → existing "not found" domain error → 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert — the row is untouched: DeletedAt / UpdatedAt keep the first deletion's values
        var instanceAfter = await client.GetResourceInstanceFromDbAsync(instance.Id);
        Assert.NotNull(instanceAfter);
        Assert.Equal(instanceBefore!.DeletedAt, instanceAfter.DeletedAt);
        Assert.Equal(instanceBefore.UpdatedAt, instanceAfter.UpdatedAt);

        // Assert — the schema stays active
        var typeAfter = await client.GetResourceSchemaFromDbAsync(resourceSchema.Id);
        Assert.NotNull(typeAfter);
        Assert.Null(typeAfter.DeletedAt);

        // Assert — no lifecycle event is published
        Assert.Empty(Factory.PublishedMessages.OfType<ResourceInstanceDeletedEvent>());
        Assert.Empty(Factory.PublishedMessages.OfType<ResourceSchemaDeletedEvent>());
        Assert.DoesNotContain(
            Factory.PublishedMessages.Published,
            m => m.GetType().Namespace == typeof(ResourceInstanceDeletedEvent).Namespace);
    }

    // -----------------------------------------------------------------------------------------
    // Arrange helpers — EF seeding of the soft-deleted state through the domain Delete(now) methods
    // -----------------------------------------------------------------------------------------

    private async Task SoftDeleteResourceSchemaAndInstancesInDbAsync(Guid typeId)
    {
        var now = DateTime.UtcNow.AddMinutes(-5);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();

        var resourceSchema = await db.ResourceSchemas.IgnoreQueryFilters().SingleAsync(t => t.Id == typeId);
        resourceSchema.Delete(now);

        var instances = await db.ResourceInstances
            .IgnoreQueryFilters()
            .Where(i => i.ResourceSchemaId == typeId)
            .ToListAsync();
        foreach (var instance in instances)
            instance.Delete(now);

        await db.SaveChangesAsync();
    }

    private async Task SoftDeleteResourceInstanceInDbAsync(Guid instanceId)
    {
        var now = DateTime.UtcNow.AddMinutes(-5);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();

        var instance = await db.ResourceInstances.IgnoreQueryFilters().SingleAsync(i => i.Id == instanceId);
        instance.Delete(now);

        await db.SaveChangesAsync();
    }
}
