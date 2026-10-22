using EventCalendar.Data;
using EventCalendar.Models;
using EventCalendar.Services;
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
        _factory = new TestApplicationFactory(_path, new Dictionary<string, string?>
        {
            ["EventCalendar:PublicBaseUrl"] = "https://calendar.example"
        });
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
        var template = _factory.Services.GetRequiredService<GameTemplateCatalog>().Find("magic")!;
        var gameEvent = template.CreateEvent("Finals", "Standard", new DateTime(2026, 10, 23, 18, 0, 0), 8, "Store").Event!;
        var eventId = await repository.CreateEventAsync(gameEvent);

        var response = await client.GetAsync($"/events/{eventId}?handler=Calendar");
        var calendar = await response.Content.ReadAsStringAsync();
        var unfoldedCalendar = calendar.Replace("\r\n ", "");

        Assert.Contains("DTSTART;TZID=America/Los_Angeles:20261023T180000", calendar);
        Assert.Contains("DTEND;TZID=America/Los_Angeles:20261023T210000", calendar);
        Assert.Contains("BEGIN:VTIMEZONE", calendar);
        Assert.Contains("TZID:America/Los_Angeles", calendar);
        Assert.Contains($"https://calendar.example/events/{eventId}/register", unfoldedCalendar);
    }

    [Fact]
    public async Task EventPage_UsesPublicBaseUrlForRegistrationLink()
    {
        using var client = _factory.CreateClient();
        var repository = _factory.Services.GetRequiredService<EventRepository>();
        var template = _factory.Services.GetRequiredService<GameTemplateCatalog>().Find("magic")!;
        var gameEvent = template.CreateEvent("Finals", "Standard", DateTime.Now.AddDays(1), 8, "Store").Event!;
        var eventId = await repository.CreateEventAsync(gameEvent);

        var html = await client.GetStringAsync($"/events/{eventId}");

        Assert.Contains($"https://calendar.example/events/{eventId}/register", html);
        Assert.Contains("<svg", html);
    }

    [Fact]
    public async Task EventPage_FallsBackToStoredTemplateIdWhenTemplateIsRetired()
    {
        using var client = _factory.CreateClient();
        var repository = _factory.Services.GetRequiredService<EventRepository>();
        var retiredTemplate = new GameTemplate("retired-game", "Retired Game", ["Legacy"], 90, 8, 12, 2);
        var gameEvent = retiredTemplate.CreateEvent("Old Event", "Legacy", DateTime.Now.AddDays(1), 8, "Store").Event!;
        var eventId = await repository.CreateEventAsync(gameEvent);

        var response = await client.GetAsync($"/events/{eventId}");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("retired-game", html);
        Assert.Contains("Legacy", html);
    }
}
