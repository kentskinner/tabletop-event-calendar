using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventCalendar.Tests;

public sealed class CreateEventTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"calendar-http-{Guid.NewGuid()}.db");
    private WebApplicationFactory<Program> _factory = null!;

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
    public async Task InvalidGameTemplate_ReturnsValidationMessage()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var getResponse = await client.GetAsync("/Events/Create");
        var getHtml = await getResponse.Content.ReadAsStringAsync();
        var token = Regex.Match(getHtml, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

        var response = await client.PostAsync("/Events/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Name"] = "Invalid game",
            ["Input.GameTemplateId"] = "warhammer",
            ["Input.Format"] = "Standard",
            ["Input.StartLocal"] = "2026-10-23T18:00",
            ["Input.Capacity"] = "16",
            ["Input.Location"] = "Store",
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Choose a valid game.", await response.Content.ReadAsStringAsync());
    }

    private sealed class TestApplicationFactory(string databasePath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Events"] = $"Data Source={databasePath};Default Timeout=10"
                }));
    }
}
