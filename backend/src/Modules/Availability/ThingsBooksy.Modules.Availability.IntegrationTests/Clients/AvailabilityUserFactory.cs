using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using ThingsBooksy.Shared.Infrastructure.Auth;
using ThingsBooksy.Shared.Infrastructure.Postgres;
using ThingsBooksy.Shared.IntegrationTests;
using ThingsBooksy.Shared.IntegrationTests.Clients;

namespace ThingsBooksy.Modules.Availability.IntegrationTests.Clients;

/// <summary>
/// Creates authenticated users for Availability integration tests by inserting a row directly
/// into management_groups.user_read_models via raw SQL and generating a valid JWT.
/// </summary>
public sealed class AvailabilityUserFactory
{
    private readonly ThingsBooksyWebAppFactory _factory;

    public AvailabilityUserFactory(ThingsBooksyWebAppFactory factory)
        => _factory = factory;

    public async Task<AuthenticatedUser> CreateUserAsync(string email)
    {
        var userId = Guid.CreateVersion7();

        var connectionString = GetConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            """INSERT INTO management_groups.user_read_models ("Id", "Email") VALUES (@id, @email)""",
            connection);
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("email", email.ToLowerInvariant());
        await cmd.ExecuteNonQueryAsync();

        using var scope = _factory.Services.CreateScope();
        var authOptions = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.Jwt.IssuerSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Aud, authOptions.Jwt.Audience),
            new(ClaimTypes.Role, "user"),
        };

        var token = new JwtSecurityToken(
            issuer: authOptions.Jwt.Issuer,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);

        return new AuthenticatedUser(client, userId, email);
    }

    private string GetConnectionString()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<IOptions<PostgresOptions>>().Value.ConnectionString;
    }
}
