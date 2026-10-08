using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ThingsBooksy.Modules.Resources.IntegrationTests.Clients;
using ThingsBooksy.Shared.Abstractions.Events;
using ThingsBooksy.Shared.Abstractions.Events.Resources;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Resources.IntegrationTests.Events;

/// <summary>
/// Acceptance tests for the Resources lifecycle events (story 015, User Story 1: AC-3..AC-7).
///
/// Arrange = EF seeding through factories, Act = HTTP, Assert = DB re-read (IgnoreQueryFilters)
/// plus the messages recorded by the test-side <c>RecordingMessageBroker</c>
/// (<see cref="ThingsBooksyWebAppFactory.PublishedMessages"/>).
/// </summary>
[Collection("IntegrationTestCollection")]
public class ResourceLifecycleEventTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ResourcesUserFactory _users;
    private readonly ResourcesGroupReadModelFactory _groups;
    private readonly ResourcesResourceSchemaFactory _types;
    private readonly ResourcesResourceInstanceFactory _instances;

    public ResourceLifecycleEventTests(ThingsBooksyWebAppFactory factory) : base(factory)
    {
        _users = new ResourcesUserFactory(factory);
        _groups = new ResourcesGroupReadModelFactory(factory);
        _types = new ResourcesResourceSchemaFactory(factory);
        _instances = new ResourcesResourceInstanceFactory(factory);
    }

    // -----------------------------------------------------------------------------------------
    // AC-3 — POST /resources/schemas publishes ResourceSchemaCreatedEvent(SchemaId, GroupId)
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "015/AC-3")]
    public async Task CreateResourceSchema_WithValidData_PublishesResourceSchemaCreatedEvent()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("evt_creatert_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);
        Factory.PublishedMessages.Clear();

        // Act
        var response = await client.CreateResourceSchemaAsync(group.Id, "Meeting Room", "Schema with an event");

        // Assert — 201 with the new id
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateResourceSchemaResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);

        // Assert — the row is persisted
        var resourceSchema = await client.GetResourceSchemaFromDbIgnoringFiltersAsync(body.Id);
        Assert.NotNull(resourceSchema);
        Assert.Equal(group.Id, resourceSchema.GroupId);
        Assert.Null(resourceSchema.DeletedAt);

        // Assert — exactly one ResourceSchemaCreatedEvent carrying the new schema id and its group id
        var created = Factory.PublishedMessages.OfType<ResourceSchemaCreatedEvent>();
        var evt = Assert.Single(created);
        Assert.Equal(resourceSchema.Id, evt.SchemaId);
        Assert.Equal(resourceSchema.GroupId, evt.GroupId);
    }

    // -----------------------------------------------------------------------------------------
    // AC-4 — POST /resources/instances publishes ResourceInstanceCreatedEvent(ResourceId, SchemaId)
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "015/AC-4")]
    public async Task CreateResourceInstance_WithValidData_PublishesResourceInstanceCreatedEvent()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("evt_createri_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var resourceSchema = await _types.CreateResourceSchemaAsync(group.Id, owner.UserId, "Desk");
        var client = new ResourcesTestClient(Factory, owner);
        Factory.PublishedMessages.Clear();

        // Act
        var response = await client.CreateResourceInstanceAsync(resourceSchema.Id, "Desk 1");

        // Assert — 201 with the new id
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateResourceInstanceResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);

        // Assert — the row is persisted under the seeded schema
        var instance = await client.GetResourceInstanceFromDbAsync(body.Id);
        Assert.NotNull(instance);
        Assert.Equal(resourceSchema.Id, instance.ResourceSchemaId);
        Assert.Null(instance.DeletedAt);

        // Assert — exactly one ResourceInstanceCreatedEvent carrying the new instance id and its schema id
        var created = Factory.PublishedMessages.OfType<ResourceInstanceCreatedEvent>();
        var evt = Assert.Single(created);
        Assert.Equal(instance.Id, evt.ResourceId);
        Assert.Equal(instance.ResourceSchemaId, evt.SchemaId);
    }

    // -----------------------------------------------------------------------------------------
    // AC-5 — DELETE /resources/schemas/{id} with instances: exactly one ResourceSchemaDeletedEvent,
    //        no ResourceInstanceDeletedEvent for the instances soft-deleted as a side effect
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "015/AC-5")]
    public async Task DeleteResourceSchema_WithInstances_PublishesExactlyOneResourceSchemaDeletedEvent()
    {
        // Arrange — schema with 2 instances
        var owner = await _users.CreateUserAsync("evt_deletert_inst_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var resourceSchema = await _types.CreateResourceSchemaAsync(group.Id, owner.UserId, "Car");
        var instanceA = await _instances.CreateResourceInstanceAsync(resourceSchema, owner.UserId, "Car A");
        var instanceB = await _instances.CreateResourceInstanceAsync(resourceSchema, owner.UserId, "Car B");
        var client = new ResourcesTestClient(Factory, owner);
        Factory.PublishedMessages.Clear();

        // Act
        var response = await client.DeleteResourceSchemaAsync(resourceSchema.Id);

        // Assert — 204
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Assert — the side effect happened: both instances are soft-deleted in the DB
        var instancesAfter = await client.GetInstancesFromDbIgnoringFiltersAsync(resourceSchema.Id);
        Assert.Equal(2, instancesAfter.Count);
        Assert.All(instancesAfter, i => Assert.NotNull(i.DeletedAt));

        // Assert — exactly one ResourceSchemaDeletedEvent(SchemaId, GroupId)
        var schemaDeleted = Factory.PublishedMessages.OfType<ResourceSchemaDeletedEvent>();
        var evt = Assert.Single(schemaDeleted);
        Assert.Equal(resourceSchema.Id, evt.SchemaId);
        Assert.Equal(group.Id, evt.GroupId);

        // Assert — no per-instance events for the instances deleted as a side effect
        var instanceDeleted = Factory.PublishedMessages.OfType<ResourceInstanceDeletedEvent>();
        Assert.DoesNotContain(instanceDeleted, e => e.ResourceId == instanceA.Id);
        Assert.DoesNotContain(instanceDeleted, e => e.ResourceId == instanceB.Id);
        Assert.Empty(instanceDeleted);
    }

    [Fact]
    [Trait("AC", "015/AC-5")]
    public async Task DeleteResourceSchema_WithoutInstances_PublishesOneResourceSchemaDeletedEvent()
    {
        // Arrange — schema with no instances (N = 0)
        var owner = await _users.CreateUserAsync("evt_deletert_noinst_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var resourceSchema = await _types.CreateResourceSchemaAsync(group.Id, owner.UserId, "Projector");
        var client = new ResourcesTestClient(Factory, owner);
        Factory.PublishedMessages.Clear();

        // Act
        var response = await client.DeleteResourceSchemaAsync(resourceSchema.Id);

        // Assert — 204
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Assert — exactly one ResourceSchemaDeletedEvent(SchemaId, GroupId), nothing per instance
        var evt = Assert.Single(Factory.PublishedMessages.OfType<ResourceSchemaDeletedEvent>());
        Assert.Equal(resourceSchema.Id, evt.SchemaId);
        Assert.Equal(group.Id, evt.GroupId);
        Assert.Empty(Factory.PublishedMessages.OfType<ResourceInstanceDeletedEvent>());
    }

    // -----------------------------------------------------------------------------------------
    // AC-6 — DELETE /resources/instances/{id} publishes ResourceInstanceDeletedEvent(ResourceId, SchemaId)
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "015/AC-6")]
    public async Task DeleteResourceInstance_Existing_PublishesResourceInstanceDeletedEvent()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("evt_deleteri_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var resourceSchema = await _types.CreateResourceSchemaAsync(group.Id, owner.UserId, "Bike");
        var instance = await _instances.CreateResourceInstanceAsync(resourceSchema, owner.UserId, "Bike 1");
        var client = new ResourcesTestClient(Factory, owner);
        Factory.PublishedMessages.Clear();

        // Act
        var response = await client.DeleteResourceInstanceAsync(instance.Id);

        // Assert — 204
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Assert — the instance is soft-deleted in the DB
        var instanceAfter = await client.GetResourceInstanceFromDbAsync(instance.Id);
        Assert.NotNull(instanceAfter);
        Assert.NotNull(instanceAfter.DeletedAt);

        // Assert — exactly one ResourceInstanceDeletedEvent carrying the instance id and its schema id
        var evt = Assert.Single(Factory.PublishedMessages.OfType<ResourceInstanceDeletedEvent>());
        Assert.Equal(instance.Id, evt.ResourceId);
        Assert.Equal(resourceSchema.Id, evt.SchemaId);
        Assert.Equal(instanceAfter.ResourceSchemaId, evt.SchemaId);
    }

    // -----------------------------------------------------------------------------------------
    // AC-7 — the events that leave Resources are the Shared.Abstractions/Events/Resources contracts
    //        (public positional records implementing IEvent, Guid-only payloads) and nothing else
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("AC", "015/AC-7")]
    public async Task PublishResourceEvents_ThroughLifecycleCommands_UseOnlySharedAbstractionsContracts()
    {
        // Arrange
        var owner = await _users.CreateUserAsync("evt_contracts_owner@test.com");
        var group = await _groups.CreateGroupReadModelAsync(owner.UserId);
        var client = new ResourcesTestClient(Factory, owner);
        Factory.PublishedMessages.Clear();

        // Act — the four lifecycle commands through HTTP
        var createTypeResponse = await client.CreateResourceSchemaAsync(group.Id, "Kayak");
        Assert.Equal(HttpStatusCode.Created, createTypeResponse.StatusCode);
        var typeId = (await createTypeResponse.Content.ReadFromJsonAsync<CreateResourceSchemaResponse>(JsonOptions))!.Id;

        var createInstanceResponse = await client.CreateResourceInstanceAsync(typeId, "Kayak 1");
        Assert.Equal(HttpStatusCode.Created, createInstanceResponse.StatusCode);
        var instanceId = (await createInstanceResponse.Content.ReadFromJsonAsync<CreateResourceInstanceResponse>(JsonOptions))!.Id;

        var deleteInstanceResponse = await client.DeleteResourceInstanceAsync(instanceId);
        Assert.Equal(HttpStatusCode.NoContent, deleteInstanceResponse.StatusCode);

        var deleteTypeResponse = await client.DeleteResourceSchemaAsync(typeId);
        Assert.Equal(HttpStatusCode.NoContent, deleteTypeResponse.StatusCode);

        // Assert — exactly the four contracts were published, one each
        var published = Factory.PublishedMessages.Published;
        var expectedTypes = new[]
        {
            typeof(ResourceSchemaCreatedEvent),
            typeof(ResourceInstanceCreatedEvent),
            typeof(ResourceInstanceDeletedEvent),
            typeof(ResourceSchemaDeletedEvent),
        };
        Assert.Equal(expectedTypes.Length, published.Count);
        Assert.Equal(
            expectedTypes.Select(t => t.FullName).OrderBy(n => n),
            published.Select(m => m.GetType().FullName).OrderBy(n => n));

        // Assert — every published type is a Shared.Abstractions contract under Events/Resources,
        //          a public positional record implementing IEvent with Guid-only parameters
        foreach (var message in published)
        {
            var type = message.GetType();

            Assert.Equal("ThingsBooksy.Shared.Abstractions", type.Assembly.GetName().Name);
            Assert.Equal("ThingsBooksy.Shared.Abstractions.Events.Resources", type.Namespace);
            Assert.True(type.IsPublic, $"{type.Name} must be public");
            Assert.True(typeof(IEvent).IsAssignableFrom(type), $"{type.Name} must implement IEvent");
            Assert.NotNull(type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance));

            var ctor = Assert.Single(type.GetConstructors(), c => c.GetParameters().All(p => p.ParameterType != type));
            var parameters = ctor.GetParameters();
            Assert.Equal(2, parameters.Length);
            Assert.All(parameters, p => Assert.Equal(typeof(Guid), p.ParameterType));
        }

        // Assert — payload values match the persisted rows
        var schemaCreated = published.OfType<ResourceSchemaCreatedEvent>().Single();
        Assert.Equal(new ResourceSchemaCreatedEvent(typeId, group.Id), schemaCreated);
        Assert.Equal(new ResourceInstanceCreatedEvent(instanceId, typeId), published.OfType<ResourceInstanceCreatedEvent>().Single());
        Assert.Equal(new ResourceInstanceDeletedEvent(instanceId, typeId), published.OfType<ResourceInstanceDeletedEvent>().Single());
        Assert.Equal(new ResourceSchemaDeletedEvent(typeId, group.Id), published.OfType<ResourceSchemaDeletedEvent>().Single());
    }
}
