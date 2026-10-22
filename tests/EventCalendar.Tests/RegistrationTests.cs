using EventCalendar.Data;
using EventCalendar.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventCalendar.Tests;

public sealed class RegistrationTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"calendar-{Guid.NewGuid()}.db");
    private EventRepository _repository = null!;

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
        var eventId = await _repository.CreateEventAsync(new(0, "Finals", "magic", "Standard", DateTime.Now, 120, 1, 1, "Store"));
        var attempts = await Task.WhenAll(_repository.RegisterAsync(eventId, "Ava"), _repository.RegisterAsync(eventId, "Ben"));

        Assert.Single(attempts, x => x.Status == RegistrationStatus.Confirmed);
        Assert.Single(attempts, x => x.Status == RegistrationStatus.Full);
        Assert.Equal(1, (await _repository.GetEventAsync(eventId))!.RegistrationCount);
    }

    [Fact]
    public async Task DuplicateName_IsRejectedCaseInsensitively()
    {
        var eventId = await _repository.CreateEventAsync(new(0, "League", "pokemon", "Standard", DateTime.Now, 120, 4, 2, "Store"));
        Assert.Equal(RegistrationStatus.Confirmed, (await _repository.RegisterAsync(eventId, "Mina")).Status);
        Assert.Equal(RegistrationStatus.Duplicate, (await _repository.RegisterAsync(eventId, "mina")).Status);
    }
}
