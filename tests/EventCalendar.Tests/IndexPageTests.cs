using EventCalendar.Data;
using EventCalendar.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventCalendar.Tests;

public sealed class IndexPageTests : IAsyncLifetime
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
    public async Task Agenda_ShowsUpcomingEventsAndHidesPastEvents()
    {
        using var client = _factory.CreateClient();
        var repository = _factory.Services.GetRequiredService<EventRepository>();
        var template = _factory.Services.GetRequiredService<GameTemplateCatalog>().Find("magic")!;
        await repository.CreateEventAsync(template.CreateEvent(
            "Past Event", "Standard", DateTime.Today.AddDays(-1).AddHours(18), 8, "Store").Event!);
        await repository.CreateEventAsync(template.CreateEvent(
            "Upcoming Event", "Standard", DateTime.Today.AddDays(1).AddHours(18), 8, "Store").Event!);

        var html = await client.GetStringAsync("/");

        Assert.DoesNotContain("Past Event", html);
        Assert.Contains("Upcoming Event", html);
    }
}
