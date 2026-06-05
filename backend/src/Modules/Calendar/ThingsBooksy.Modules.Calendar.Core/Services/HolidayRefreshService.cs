using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThingsBooksy.Modules.Calendar.Core.DAL;
using ThingsBooksy.Modules.Calendar.Core.Domain;
using ThingsBooksy.Shared.Abstractions.EventPayloads.Calendar;
using ThingsBooksy.Shared.Abstractions.Events.Calendar;
using ThingsBooksy.Shared.Abstractions.Messaging;

namespace ThingsBooksy.Modules.Calendar.Core.Services;

internal sealed class HolidayRefreshService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessageBroker _messageBroker;
    private readonly ILogger<HolidayRefreshService> _logger;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromHours(24));
    private Task? _backgroundTask;
    private CancellationTokenSource? _cts;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public HolidayRefreshService(
        IServiceScopeFactory scopeFactory,
        IMessageBroker messageBroker,
        ILogger<HolidayRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _messageBroker = messageBroker;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _backgroundTask = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
            await _cts.CancelAsync();

        if (_backgroundTask is not null)
        {
            try
            {
                await _backgroundTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        await TryRefreshAsync(ct);

        while (await _timer.WaitForNextTickAsync(ct))
        {
            await TryRefreshAsync(ct);
        }
    }

    private async Task TryRefreshAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();

            var alreadyFetched = await db.FetchLogs.AnyAsync(x => x.Year == year, ct);
            if (alreadyFetched)
            {
                _logger.LogDebug("Public holidays for {Year} already fetched — skipping.", year);
                return;
            }

            var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var client = httpFactory.CreateClient("nager-date");

            var url = $"api/v3/PublicHolidays/{year}/PL";
            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var raw = JsonSerializer.Deserialize<List<NagerHolidayItem>>(json, JsonOptions)
                ?? [];

            var holidays = raw
                .Select(h => new PublicHolidayDto(h.Date, h.LocalName, h.Name))
                .ToList()
                .AsReadOnly();

            var now = DateTime.UtcNow;
            var log = PublicHolidayFetchLog.Create(year, now, holidays.Count);
            db.FetchLogs.Add(log);
            await db.SaveChangesAsync(ct);

            await _messageBroker.PublishAsync(new HolidaysRefreshedEvent(year, holidays), ct);

            _logger.LogInformation(
                "Fetched {Count} public holidays for {Year} and published HolidaysRefreshedEvent.",
                holidays.Count, year);
        }
        catch (OperationCanceledException)
        {
            // Shutting down — normal
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh public holidays for {Year}.", year);
        }
    }

    private sealed record NagerHolidayItem(
        [property: JsonPropertyName("date")] DateOnly Date,
        [property: JsonPropertyName("localName")] string LocalName,
        [property: JsonPropertyName("name")] string Name);
}
