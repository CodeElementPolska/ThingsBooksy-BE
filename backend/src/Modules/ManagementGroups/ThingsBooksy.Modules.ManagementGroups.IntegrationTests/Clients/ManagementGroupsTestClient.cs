using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using ThingsBooksy.Modules.ManagementGroups.Core.DAL;
using ThingsBooksy.Modules.ManagementGroups.Core.Domain;
using ThingsBooksy.Modules.ManagementGroups.Core.ReadModels;
using ThingsBooksy.Shared.Infrastructure.Postgres;
using ThingsBooksy.Shared.IntegrationTests;
using ThingsBooksy.Shared.IntegrationTests.Clients;

namespace ThingsBooksy.Modules.ManagementGroups.IntegrationTests.Clients;

public record CreateGroupResponse(Guid Id);

public class ManagementGroupsTestClient
{
    private readonly HttpClient _client;
    private readonly ThingsBooksyWebAppFactory _factory;

    public ManagementGroupsTestClient(ThingsBooksyWebAppFactory factory, AuthenticatedUser user)
    {
        _factory = factory;
        _client = user.Client;
    }

    // --- HTTP methods (Arrange / Act) ---

    public Task<HttpResponseMessage> CreateGroupAsync(string name, string? description = null)
        => _client.PostAsJsonAsync("/management-groups", new { Name = name, Description = description });

    public async Task<Guid> CreateGroupAndGetIdAsync(string name, string? description = null)
    {
        var response = await CreateGroupAsync(name, description);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateGroupResponse>();
        return result!.Id;
    }

    public Task<HttpResponseMessage> GetGroupsAsync()
        => _client.GetAsync("/management-groups");

    public Task<HttpResponseMessage> GetGroupAsync(Guid id)
        => _client.GetAsync($"/management-groups/{id}");

    public Task<HttpResponseMessage> UpdateGroupAsync(Guid id, string name, string? description = null)
        => _client.PutAsJsonAsync($"/management-groups/{id}", new { Name = name, Description = description });

    public Task<HttpResponseMessage> DeleteGroupAsync(Guid id)
        => _client.DeleteAsync($"/management-groups/{id}");

    public Task<HttpResponseMessage> RestoreGroupAsync(Guid id)
        => _client.PostAsJsonAsync($"/management-groups/{id}/restore", new { });

    public Task<HttpResponseMessage> AddMemberAsync(Guid groupId, string email)
        => _client.PostAsJsonAsync($"/management-groups/{groupId}/members", new { Email = email });

    public Task<HttpResponseMessage> RemoveMemberAsync(Guid groupId, Guid userId)
        => _client.DeleteAsync($"/management-groups/{groupId}/members/{userId}");

    public Task<HttpResponseMessage> IsGroupNameAvailableAsync(string name)
        => _client.GetAsync($"/management-groups/name-available?name={Uri.EscapeDataString(name)}");

    public Task<HttpResponseMessage> GetGroupMembersAsync(Guid groupId, Guid? afterId = null, int? take = null)
    {
        var qs = new List<string>();
        if (afterId.HasValue) qs.Add($"afterId={afterId}");
        if (take.HasValue) qs.Add($"take={take}");
        var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";
        return _client.GetAsync($"/management-groups/{groupId}/members{query}");
    }

    // --- DB methods (Assert) ---

    internal async Task<ManagementGroup?> GetGroupFromDbAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ManagementGroupsDbContext>();
        return await db.ManagementGroups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    internal async Task<List<GroupMember>> GetMembersFromDbAsync(Guid groupId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ManagementGroupsDbContext>();
        return await db.GroupMembers
            .Where(x => x.GroupId == groupId)
            .ToListAsync();
    }

    internal async Task<UserReadModel?> GetUserReadModelFromDbAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ManagementGroupsDbContext>();
        return await db.UserReadModels
            .FirstOrDefaultAsync(x => x.Email == email);
    }

    // --- Resources schema DB helpers (raw SQL — no cross-module type import) ---

    internal async Task<bool> ResourcesGroupReadModelExistsAsync(Guid groupId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """SELECT COUNT(1) FROM resources.group_read_models WHERE "Id" = @id""",
            connection);
        cmd.Parameters.AddWithValue("id", groupId);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return count > 0;
    }

    internal async Task<bool> ResourcesGroupReadModelAbsentAsync(Guid groupId)
        => !await ResourcesGroupReadModelExistsAsync(groupId);

    internal async Task<bool> ResourcesAllResourceSchemasDeletedAsync(Guid groupId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """SELECT COUNT(1) FROM resources.resource_schemas WHERE "GroupId" = @groupId""",
            connection);
        cmd.Parameters.AddWithValue("groupId", groupId);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return count == 0;
    }

    internal async Task<bool> ResourcesAllResourceInstancesSoftDeletedAsync(Guid groupId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """SELECT COUNT(1) FROM resources.resource_instances WHERE "GroupId" = @groupId AND "DeletedAt" IS NULL""",
            connection);
        cmd.Parameters.AddWithValue("groupId", groupId);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return count == 0;
    }

    internal async Task<int> ResourcesResourceInstanceCountAsync(Guid groupId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """SELECT COUNT(1) FROM resources.resource_instances WHERE "GroupId" = @groupId""",
            connection);
        cmd.Parameters.AddWithValue("groupId", groupId);
        return (int)(long)(await cmd.ExecuteScalarAsync())!;
    }

    internal async Task<bool> ResourcesGroupMemberReadModelExistsAsync(Guid groupId, Guid userId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """SELECT COUNT(1) FROM resources.group_member_read_models WHERE "GroupId" = @groupId AND "UserId" = @userId""",
            connection);
        cmd.Parameters.AddWithValue("groupId", groupId);
        cmd.Parameters.AddWithValue("userId", userId);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return count > 0;
    }

    internal async Task<bool> ResourcesGroupMemberReadModelAbsentAsync(Guid groupId, Guid userId)
        => !await ResourcesGroupMemberReadModelExistsAsync(groupId, userId);

    private string GetConnectionString()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<IOptions<PostgresOptions>>().Value.ConnectionString;
    }
}
