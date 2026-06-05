using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Modules.Availability.Core.DAL;
using ThingsBooksy.Modules.Availability.Core.Domain;
using ThingsBooksy.Modules.Availability.Core.ReadModels;
using ThingsBooksy.Shared.IntegrationTests;
using ThingsBooksy.Shared.IntegrationTests.Clients;

namespace ThingsBooksy.Modules.Availability.IntegrationTests.Clients;

public record AvailabilityRulesResponse(int BufferMinutes, List<AvailabilityRuleDto> Rules, List<AvailabilityRuleDto>? InheritedRules);
public record AvailabilityRuleDto(Guid RuleId, string RuleType, string RuleMode, int[]? DaysOfWeek, string StartTime, string EndTime, int EndDayOffset, string? StartDate, string? EndDate);

/// <summary>
/// Test client for Availability module integration tests.
/// Wraps HTTP calls to /availability/... endpoints and provides DB assertion helpers.
/// </summary>
public class AvailabilityTestClient
{
    private readonly HttpClient _client;
    private readonly ThingsBooksyWebAppFactory _factory;

    public AvailabilityTestClient(ThingsBooksyWebAppFactory factory, AuthenticatedUser user)
    {
        _factory = factory;
        _client = user.Client;
    }

    // -----------------------------------------------------------------------------------------
    // HTTP methods
    // -----------------------------------------------------------------------------------------

    public Task<HttpResponseMessage> GetSchemaRulesAsync(Guid schemaId)
        => _client.GetAsync($"/availability/schemas/{schemaId}/rules");

    public Task<HttpResponseMessage> PutSchemaRulesAsync(Guid schemaId, object body)
        => _client.PutAsJsonAsync($"/availability/schemas/{schemaId}/rules", body);

    public Task<HttpResponseMessage> GetResourceRulesAsync(Guid resourceId)
        => _client.GetAsync($"/availability/resources/{resourceId}/rules");

    public Task<HttpResponseMessage> PutResourceRulesAsync(Guid resourceId, object body)
        => _client.PutAsJsonAsync($"/availability/resources/{resourceId}/rules", body);

    // -----------------------------------------------------------------------------------------
    // Response deserialization helpers
    // -----------------------------------------------------------------------------------------

    public async Task<AvailabilityRulesResponse?> ReadRulesResponseAsync(HttpResponseMessage response)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return await response.Content.ReadFromJsonAsync<AvailabilityRulesResponse>(options);
    }

    // -----------------------------------------------------------------------------------------
    // DB assertion helpers
    // -----------------------------------------------------------------------------------------

    internal async Task<SchemaRuleSet?> GetSchemaRuleSetFromDbAsync(Guid schemaId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();
        return await db.SchemaRuleSets
            .Include(x => x.Rules)
            .FirstOrDefaultAsync(x => x.SchemaId == schemaId);
    }

    internal async Task<ResourceRuleSet?> GetResourceRuleSetFromDbAsync(Guid resourceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();
        return await db.ResourceRuleSets
            .Include(x => x.Rules)
            .FirstOrDefaultAsync(x => x.ResourceId == resourceId);
    }

    internal async Task<GroupReadModel?> GetGroupReadModelFromDbAsync(Guid groupId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();
        return await db.GroupReadModels.FirstOrDefaultAsync(x => x.Id == groupId);
    }

    internal async Task<SchemaReadModel?> GetSchemaReadModelFromDbAsync(Guid schemaId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();
        return await db.SchemaReadModels.FirstOrDefaultAsync(x => x.Id == schemaId);
    }

    internal async Task<ResourceReadModel?> GetResourceReadModelFromDbAsync(Guid resourceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();
        return await db.ResourceReadModels.FirstOrDefaultAsync(x => x.Id == resourceId);
    }

    // -----------------------------------------------------------------------------------------
    // Polling helpers for async event handlers
    // -----------------------------------------------------------------------------------------

    internal async Task<bool> GroupReadModelExistsAsync(Guid groupId)
        => await GetGroupReadModelFromDbAsync(groupId) is not null;

    internal async Task<bool> SchemaReadModelExistsAsync(Guid schemaId)
        => await GetSchemaReadModelFromDbAsync(schemaId) is not null;

    internal async Task<bool> ResourceReadModelExistsAsync(Guid resourceId)
        => await GetResourceReadModelFromDbAsync(resourceId) is not null;
}
