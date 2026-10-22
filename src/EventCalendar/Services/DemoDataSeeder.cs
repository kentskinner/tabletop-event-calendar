using EventCalendar.Data;
using EventCalendar.Models;

namespace EventCalendar.Services;

public sealed class DemoDataSeeder(EventRepository repository, GameTemplateCatalog catalog)
{
    public async Task SeedAsync()
    {
        if ((await repository.ListEventsAsync()).Count > 0)
            return;

        var magic = catalog.Find("magic")!;
        var draftId = await repository.CreateEventAsync(CreateEvent(
            "Friday Night Draft", magic, "Draft", DateTime.Today.AddDays(1).AddHours(18), 8, "Downtown Game Store"));

        foreach (var player in new[] { "Avery", "Blake", "Casey", "Drew", "Emery", "Finley" })
            await repository.RegisterAsync(draftId, player);

        var pokemon = catalog.Find("pokemon")!;
        await repository.CreateEventAsync(CreateEvent(
            "Pokémon League Cup", pokemon, "Standard", DateTime.Today.AddDays(3).AddHours(13), 16, "Downtown Game Store"));

        var lorcana = catalog.Find("lorcana")!;
        await repository.CreateEventAsync(CreateEvent(
            "Lorcana Sealed Night", lorcana, "Sealed", DateTime.Today.AddDays(6).AddHours(18).AddMinutes(30), 12, "Downtown Game Store"));
    }

    private static GameEvent CreateEvent(
        string name,
        GameTemplate template,
        string format,
        DateTime start,
        int capacity,
        string location) =>
        new(0, name, template.Id, format, start, template.DefaultDurationMinutes,
            capacity, template.MinimumPlayers, location);
}
