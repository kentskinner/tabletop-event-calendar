using EventCalendar.Data;
using EventCalendar.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventCalendar.Tests;

public sealed class CalendarInviteTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"calendar-http-{Guid.NewGuid()}.db");
    private TestApplicationFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new TestApplicationFactory(_path);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _path, $"{_path}-shm", $"{_path}-wal" })
            if (File.Exists(path)) File.Delete(path);
    }

    [Fact]
    public async Task CalendarInvite_UsesConfiguredStoreTimeZone()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var repository = _factory.Services.GetRequiredService<EventRepository>();
        var template = new GameTemplate("test", "Test Game", ["Standard"], 120, 8, 30, 1);
        var gameEvent = template.CreateEvent("Finals", "Standard", new DateTime(2026, 10, 23, 18, 0, 0), 8, "Store").Event!;
        var eventId = await repository.CreateEventAsync(gameEvent);

        var response = await client.GetAsync($"/events/{eventId}?handler=Calendar");
        var calendar = await response.Content.ReadAsStringAsync();

        Assert.Contains("DTSTART;TZID=America/Los_Angeles:20261023T180000", calendar);
        Assert.Contains("DTEND;TZID=America/Los_Angeles:20261023T200000", calendar);
    }
}
