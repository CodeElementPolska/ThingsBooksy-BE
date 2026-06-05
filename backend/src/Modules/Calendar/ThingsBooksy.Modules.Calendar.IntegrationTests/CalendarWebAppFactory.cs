using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ThingsBooksy.Shared.IntegrationTests;

namespace ThingsBooksy.Modules.Calendar.IntegrationTests;

/// <summary>
/// Extends <see cref="ThingsBooksyWebAppFactory"/> for Calendar-specific test needs.
/// Replaces the <c>"nager-date"</c> named HTTP client's primary handler with a stub
/// that returns a fixed list of Polish public holidays, ensuring no real outbound HTTP
/// calls are made during integration tests.
/// </summary>
public sealed class CalendarWebAppFactory : ThingsBooksyWebAppFactory
{
    /// <summary>
    /// Fixed mock response returned by every call to the Nager.Date API in tests.
    /// Contains 3 public holidays to cover the full parsing and persistence path.
    /// </summary>
    internal static readonly string MockHolidayJson =
        """
        [
          { "date": "2026-01-01", "localName": "Nowy Rok", "name": "New Year's Day" },
          { "date": "2026-05-01", "localName": "Święto Pracy", "name": "Labour Day" },
          { "date": "2026-11-11", "localName": "Narodowe Święto Niepodległości", "name": "Independence Day" }
        ]
        """;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Register the mock handler as a transient service so the factory can resolve it.
            services.AddTransient<NagerDateMockHandler>();

            // Re-configure the named client to use the mock primary handler.
            // ASP.NET Core honours the last ConfigurePrimaryHttpMessageHandler registration,
            // so this effectively replaces the real outbound HttpClientHandler.
            services.AddHttpClient("nager-date")
                .ConfigurePrimaryHttpMessageHandler<NagerDateMockHandler>();
        });
    }
}

/// <summary>
/// Returns the fixed <see cref="CalendarWebAppFactory.MockHolidayJson"/> payload for
/// every GET request, regardless of URL. Not thread-safe — one instance per request is
/// guaranteed because it is registered as transient.
/// </summary>
internal sealed class NagerDateMockHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                CalendarWebAppFactory.MockHolidayJson,
                Encoding.UTF8,
                "application/json"),
        };

        return Task.FromResult(response);
    }
}
