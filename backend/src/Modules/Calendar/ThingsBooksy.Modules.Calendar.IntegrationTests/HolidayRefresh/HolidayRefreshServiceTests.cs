using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThingsBooksy.Modules.Calendar.Core.DAL;
using ThingsBooksy.Modules.Calendar.Core.Domain;
using ThingsBooksy.Modules.Calendar.Core.Services;
using ThingsBooksy.Shared.Abstractions.Messaging;
using ThingsBooksy.Shared.IntegrationTests;
using Xunit;

namespace ThingsBooksy.Modules.Calendar.IntegrationTests.HolidayRefresh;

/// <summary>
/// Integration tests for <see cref="HolidayRefreshService"/> (T022–T032).
///
/// Each test creates a fresh instance of <see cref="HolidayRefreshService"/> directly
/// rather than relying on the singleton hosted-service that fires during host startup.
/// This avoids interference with the already-running background timer and allows the
/// Arrange→Act→Assert cycle to be fully controlled per test.
///
/// The <c>"nager-date"</c> named HTTP client is replaced by
/// <see cref="NagerDateMockHandler"/> in <see cref="CalendarWebAppFactory"/> so no real
/// outbound HTTP calls are ever made.
/// </summary>
public class HolidayRefreshServiceTests : IntegrationTestBase
{
    public HolidayRefreshServiceTests(CalendarWebAppFactory factory) : base(factory)
    {
    }

    // -------------------------------------------------------------------------
    // T032 — service creates a PublicHolidayFetchLog when no entry exists
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HolidayRefreshService_WhenNoLogForCurrentYear_CreatesFetchLog()
    {
        // Arrange — DB was reset by IntegrationTestBase.InitializeAsync; no fetch log exists.
        var service = CreateService();

        // Act — start the service; it calls TryRefreshAsync immediately on the first tick.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await service.StartAsync(cts.Token);

        // Assert — poll until the fetch log appears (up to 5 s).
        var logAppeared = await WaitUntilAsync(
            async () =>
            {
                var log = await GetFetchLogFromDbAsync(DateTime.UtcNow.Year);
                return log is not null;
            });

        await service.StopAsync(CancellationToken.None);

        Assert.True(logAppeared, "PublicHolidayFetchLog was not created for the current year within the timeout.");

        var fetchLog = await GetFetchLogFromDbAsync(DateTime.UtcNow.Year);
        Assert.NotNull(fetchLog);
        Assert.Equal(DateTime.UtcNow.Year, fetchLog.Year);
        Assert.Equal(3, fetchLog.HolidayCount); // mock returns exactly 3 holidays
        Assert.True(fetchLog.FetchedAt <= DateTime.UtcNow);
    }

    // -------------------------------------------------------------------------
    // T032 — service emits HolidaysRefreshedEvent (observable via fetch log)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HolidayRefreshService_WhenNoLogForCurrentYear_EmitsHolidaysRefreshedEvent()
    {
        // Arrange — DB was reset; no fetch log exists.
        // The event is published in-process after the fetch log is saved.
        // Because the Availability module (its HolidaysRefreshedHandler) is registered in the
        // same process, publishing HolidaysRefreshedEvent causes the handler to upsert
        // PublicHolidayReadModels in the Availability schema.
        // We verify that the fetch log was persisted — which only happens AFTER
        // PublishAsync is called (see HolidayRefreshService.TryRefreshAsync) — so a
        // successful log entry is a proxy for the event having been published.
        var service = CreateService();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await service.StartAsync(cts.Token);

        // Act + Assert — poll for the fetch log (proves the event-emission path ran).
        var logAppeared = await WaitUntilAsync(
            async () =>
            {
                var log = await GetFetchLogFromDbAsync(DateTime.UtcNow.Year);
                return log is not null;
            });

        await service.StopAsync(CancellationToken.None);

        Assert.True(
            logAppeared,
            "HolidayRefreshService did not complete its refresh cycle: PublicHolidayFetchLog was not found. " +
            "This indicates HolidaysRefreshedEvent was not published.");
    }

    // -------------------------------------------------------------------------
    // T032 — service does NOT create a duplicate when a log already exists
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HolidayRefreshService_WhenLogAlreadyExists_DoesNotCreateDuplicate()
    {
        // Arrange — seed a fetch log for the current year directly into the DB.
        var year = DateTime.UtcNow.Year;
        await SeedFetchLogAsync(year);

        var service = CreateService();

        // Act — start the service; it should detect the existing log and skip the fetch.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await service.StartAsync(cts.Token);

        // Give the background task time to run its first check.
        await Task.Delay(TimeSpan.FromMilliseconds(500), cts.Token);

        await service.StopAsync(CancellationToken.None);

        // Assert — exactly 1 entry in the fetch logs for the current year.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
        var count = await db.FetchLogs
            .Where(x => x.Year == year)
            .CountAsync();

        Assert.Equal(1, count);
    }

    // -------------------------------------------------------------------------
    // Infrastructure helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a fresh <see cref="HolidayRefreshService"/> instance with real DI
    /// dependencies (scoped to the test app's service provider). The named
    /// <c>"nager-date"</c> HttpClient is backed by <see cref="NagerDateMockHandler"/>
    /// so no real network calls are made.
    /// </summary>
    private HolidayRefreshService CreateService()
    {
        var scopeFactory = Factory.Services.GetRequiredService<IServiceScopeFactory>();
        var broker = Factory.Services.GetRequiredService<IMessageBroker>();
        var logger = Factory.Services.GetRequiredService<ILogger<HolidayRefreshService>>();
        return new HolidayRefreshService(scopeFactory, broker, logger);
    }

    private async Task<PublicHolidayFetchLog?> GetFetchLogFromDbAsync(int year)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
        return await db.FetchLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Year == year);
    }

    private async Task SeedFetchLogAsync(int year)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
        db.FetchLogs.Add(PublicHolidayFetchLog.Create(year, DateTime.UtcNow, 0));
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Polls <paramref name="condition"/> every 100 ms for up to 5 seconds.
    /// Returns <c>true</c> as soon as the condition is met; <c>false</c> on timeout.
    /// </summary>
    private static async Task<bool> WaitUntilAsync(
        Func<Task<bool>> condition,
        int maxAttempts = 50,
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
