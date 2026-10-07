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

namespace ThingsBooksy.Modules.Resources.IntegrationTests.ResourceSchemas;

/// <summary>
/// C5 review fixes for story 015 (User Story 2: AC-8, AC-9). These tests cover the negative half
/// of each AC: the owner-only role and the partial uniqueness still rejecting an active duplicate.
///
/// Arrange = EF seeding through factories (plus the domain Delete(now) for soft-deleted rows),
/// Act = HTTP, Assert = DB re-read with IgnoreQueryFilters plus the recorded messages.
/// </summary>
[Collection("IntegrationTestCollection")]
public class SoftDeleteResourceSchemaOwnerRuleTests : IntegrationTestBase
{
    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;
    private readonly ResourcesResourceSchemaFactory _types;
    private readonly ResourcesResourceInstanceFactory _instances;

    public SoftDeleteResourceSchemaOwnerRuleTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
        _types = new ResourcesResourceSchemaFactory(factory);
        _instances = new ResourcesResourceInstanceFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // AC-8 (role: owner) — a group member who is not the owner cannot soft-delete the schema
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "015/AC-8")]
    public async Task DeleteResourceSchema_AsNonOwnerMemberWithInstances_Returns403AndKeepsTypeAndInstancesActive()
    {
        // Arrange — schema with 2 instances in G; a member of G who is not the owner
        var owner = await _users.CreateUserAsync("softdelrt_rule_del_owner@test.com");
        var member = await _users.CreateUserAsync("softdelrt_rule_del_member@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _groups.AddGroupMemberAsync(group.Id, member.UserId);
        var resourceSchema = await _types.CreateResourceSchemaAsync(group.Id, owner.UserId, "Kayak");
        var instanceA = await _instances.CreateResourceInstanceAsync(resourceSchema, owner.UserId, "Kayak A");
        var instanceB = await _instances.CreateResourceInstanceAsync(resourceSchema, owner.UserId, "Kayak B");

        var memberClient = new ResourcesTestClient(Factory, member);
        var typeBefore = await memberClient.GetResourceSchemaFromDbIgnoringFiltersAsync(resourceSchema.Id);
        Assert.NotNull(typeBefore);
        Assert.Null(typeBefore.DeletedAt);
        Factory.PublishedMessages.Clear();

        // Act
        var response = await memberClient.DeleteResourceSchemaAsync(resourceSchema.Id);

        // Assert — 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Assert — the schema row is NOT soft-deleted (IgnoreQueryFilters read, DeletedAt still null)
        var typeAfter = await memberClient.GetResourceSchemaFromDbIgnoringFiltersAsync(resourceSchema.Id);
        Assert.NotNull(typeAfter);
        Assert.Null(typeAfter.DeletedAt);
        Assert.Equal(typeBefore.UpdatedAt, typeAfter.UpdatedAt);

        // Assert — still visible through the global query filter
        var typeFiltered = await memberClient.GetResourceSchemaFromDbRespectingQueryFiltersAsync(resourceSchema.Id);
        Assert.NotNull(typeFiltered);

        // Assert — its instances are NOT soft-deleted
        var instances = await memberClient.GetInstancesFromDbIgnoringFiltersAsync(resourceSchema.Id);
        Assert.Equal(2, instances.Count);
        Assert.Contains(instances, i => i.Id == instanceA.Id);
        Assert.Contains(instances, i => i.Id == instanceB.Id);
        Assert.All(instances, i => Assert.Null(i.DeletedAt));

        // Assert — no deletion event is published
        Assert.Empty(Factory.PublishedMessages.OfType<ResourceSchemaDeletedEvent>());
        Assert.Empty(Factory.PublishedMessages.OfType<ResourceInstanceDeletedEvent>());
    }

    // -----------------------------------------------------------------------------------------
    // AC-9 (role: owner) — a non-owner member cannot reuse the name of a soft-deleted schema
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "015/AC-9")]
    public async Task CreateResourceSchema_AsNonOwnerMemberWithNameOfSoftDeletedType_Returns403AndPersistsNothing()
    {
        // Arrange — schema "Sauna" in G, already soft-deleted; a member of G who is not the owner
        var owner = await _users.CreateUserAsync("softdelrt_rule_create_owner@test.com");
        var member = await _users.CreateUserAsync("softdelrt_rule_create_member@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        await _groups.AddGroupMemberAsync(group.Id, member.UserId);
        var original = await _types.CreateResourceSchemaAsync(group.Id, owner.UserId, "Sauna");
        await SoftDeleteResourceSchemaInDbAsync(original.Id);

        var memberClient = new ResourcesTestClient(Factory, member);
        Factory.PublishedMessages.Clear();

        // Act
        var response = await memberClient.CreateResourceSchemaAsync(group.Id, "Sauna");

        // Assert — 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Assert — the only "Sauna" row in G is the soft-deleted original (IgnoreQueryFilters)
        var rows = await memberClient.GetResourceSchemasByGroupAndNameFromDbIgnoringFiltersAsync(group.Id, "Sauna");
        var row = Assert.Single(rows);
        Assert.Equal(original.Id, row.Id);
        Assert.NotNull(row.DeletedAt);

        // Assert — no creation event is published
        Assert.Empty(Factory.PublishedMessages.OfType<ResourceSchemaCreatedEvent>());
    }

    // -----------------------------------------------------------------------------------------
    // AC-9 — uniqueness applies to non-deleted schemas: a duplicate of the ACTIVE name is still 409
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "015/AC-9")]
    public async Task CreateResourceSchema_WithNameOfActiveTypeAfterSoftDeletedPredecessor_Returns409AndKeepsOneActiveRow()
    {
        // Arrange — "Sauna" in G soft-deleted, then a new active "Sauna" in G
        var owner = await _users.CreateUserAsync("softdelrt_rule_dup_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var deleted = await _types.CreateResourceSchemaAsync(group.Id, owner.UserId, "Sauna");
        await SoftDeleteResourceSchemaInDbAsync(deleted.Id);
        var active = await _types.CreateResourceSchemaAsync(group.Id, owner.UserId, "Sauna");

        var client = new ResourcesTestClient(Factory, owner);
        Factory.PublishedMessages.Clear();

        // Act — the owner tries to create a third "Sauna" in G
        var response = await client.CreateResourceSchemaAsync(group.Id, "Sauna");

        // Assert — 409 Conflict: the active name is still unique
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // Assert — DB holds exactly the two seeded rows: one soft-deleted, one active
        var rows = await client.GetResourceSchemasByGroupAndNameFromDbIgnoringFiltersAsync(group.Id, "Sauna");
        Assert.Equal(2, rows.Count);
        var activeRow = Assert.Single(rows, r => r.DeletedAt is null);
        Assert.Equal(active.Id, activeRow.Id);
        var deletedRow = Assert.Single(rows, r => r.DeletedAt is not null);
        Assert.Equal(deleted.Id, deletedRow.Id);

        // Assert — no creation event is published
        Assert.Empty(Factory.PublishedMessages.OfType<ResourceSchemaCreatedEvent>());
    }

    // -----------------------------------------------------------------------------------------
    // Arrange helper — EF seeding of the soft-deleted state through the domain Delete(now) method
    // -----------------------------------------------------------------------------------------

    private async Task SoftDeleteResourceSchemaInDbAsync(Guid typeId)
    {
        var now = DateTime.UtcNow.AddMinutes(-5);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ResourcesDbContext>();

        var resourceSchema = await db.ResourceSchemas.IgnoreQueryFilters().SingleAsync(t => t.Id == typeId);
        resourceSchema.Delete(now);

        await db.SaveChangesAsync();
    }
}
