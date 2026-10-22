using System.Net;
using System.Text.RegularExpressions;
using EventCalendar.Data;
using EventCalendar.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventCalendar.Tests;

public sealed class RegistrationHttpTests : IAsyncLifetime
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
    public async Task RegistrationPost_RejectsPlayerPastCapacity()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var repository = _factory.Services.GetRequiredService<EventRepository>();
        var template = new GameTemplate("test", "Test Game", ["Standard"], 120, 1, 30, 1);
        var gameEvent = template.CreateEvent("Finals", "Standard", DateTime.Now.AddDays(1), 1, "Store").Event!;
        var eventId = await repository.CreateEventAsync(gameEvent);
        var token = await GetAntiforgeryTokenAsync(client, eventId);

        var firstResponse = await PostRegistrationAsync(client, eventId, "Ava", token);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);
        var confirmationResponse = await client.GetAsync(firstResponse.Headers.Location);
        Assert.Contains("You're in!", await confirmationResponse.Content.ReadAsStringAsync());

        var secondResponse = await PostRegistrationAsync(client, eventId, "Ben", token);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains("This event is full", await secondResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, (await repository.GetEventAsync(eventId))!.RegistrationCount);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, long eventId)
    {
        var getResponse = await client.GetAsync($"/events/{eventId}/register");
        var getHtml = await getResponse.Content.ReadAsStringAsync();
        return WebUtility.HtmlDecode(Regex.Match(getHtml,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value);
    }

    private static Task<HttpResponseMessage> PostRegistrationAsync(
        HttpClient client, long eventId, string playerName, string token) =>
        client.PostAsync($"/events/{eventId}/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["PlayerName"] = playerName,
            ["__RequestVerificationToken"] = token
        }));
}
