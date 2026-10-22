using EventCalendar.Data;
using EventCalendar.Models;
using EventCalendar.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventCalendar.Tests;

public sealed class RegistrationTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"calendar-{Guid.NewGuid()}.db");
    private EventRepository _repository = null!;
    private static readonly GameTemplate TestTemplate = new(
        "test", "Test Game", ["Standard"], 120, 12, 30, 1);

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Events"] = $"Data Source={_path};Default Timeout=10" }).Build();
        _repository = new EventRepository(configuration);
        await _repository.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _path, $"{_path}-shm", $"{_path}-wal" })
            if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task LastSeat_IsAwardedOnlyOnceUnderConcurrency()
    {
        var eventId = await _repository.CreateEventAsync(CreateEvent("Finals", 1));
        var attempts = await Task.WhenAll(_repository.RegisterAsync(eventId, "Ava"), _repository.RegisterAsync(eventId, "Ben"));

        Assert.Single(attempts, x => x.Status == RegistrationStatus.Confirmed);
        Assert.Single(attempts, x => x.Status == RegistrationStatus.Full);
        Assert.Equal(1, (await _repository.GetEventAsync(eventId))!.RegistrationCount);
    }

    [Fact]
    public async Task DuplicateName_IsRejectedCaseInsensitively()
    {
        var eventId = await _repository.CreateEventAsync(CreateEvent("League", 4));
        Assert.Equal(RegistrationStatus.Confirmed, (await _repository.RegisterAsync(eventId, "Mina")).Status);
        Assert.Equal(RegistrationStatus.Duplicate, (await _repository.RegisterAsync(eventId, "mina")).Status);
    }

    [Fact]
    public async Task BlankName_IsRejectedWithoutTakingASeat()
    {
        var eventId = await _repository.CreateEventAsync(CreateEvent("League", 4));

        Assert.Equal(RegistrationStatus.InvalidName, (await _repository.RegisterAsync(eventId, "   ")).Status);
        Assert.Equal(0, (await _repository.GetEventAsync(eventId))!.RegistrationCount);
    }

    [Fact]
    public async Task DeletedRegistration_FreesItsSeat()
    {
        var eventId = await _repository.CreateEventAsync(CreateEvent("League", 3));
        await _repository.RegisterAsync(eventId, "Ava");
        await _repository.RegisterAsync(eventId, "Ben");
        await _repository.RegisterAsync(eventId, "Cora");

        await using (var connection = new SqliteConnection($"Data Source={_path}"))
        {
            await connection.OpenAsync();
            var delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM registrations WHERE event_id = $event AND player_name = $name";
            delete.Parameters.AddWithValue("$event", eventId);
            delete.Parameters.AddWithValue("$name", "Ben");
            await delete.ExecuteNonQueryAsync();
        }

        var result = await _repository.RegisterAsync(eventId, "Drew");

        Assert.Equal(RegistrationStatus.Confirmed, result.Status);
        Assert.Equal(2, result.SeatNumber);
        Assert.Equal(3, (await _repository.GetEventAsync(eventId))!.RegistrationCount);
    }

    [Fact]
    public async Task DemoSeed_IsRepresentativeAndIdempotent()
    {
        var seeder = new DemoDataSeeder(_repository, new GameTemplateCatalog());

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var events = await _repository.ListEventsAsync();
        Assert.Equal(3, events.Count);
        var draft = Assert.Single(events, x => x.Name == "Friday Night Draft");
        Assert.Equal(6, draft.RegistrationCount);
        Assert.Equal(2, draft.SeatsRemaining);
        Assert.Equal(3, events.Select(x => x.GameTemplateId).Distinct().Count());
    }

    private static GameEvent CreateEvent(string name, int capacity) =>
        TestTemplate.CreateEvent(name, "Standard", DateTime.Now, capacity, "Store").Event!;
}
