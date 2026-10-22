namespace EventCalendar.Models;

public sealed record GameTemplate(
    string Id,
    string Name,
    IReadOnlyList<string> Formats,
    int DefaultDurationMinutes,
    int DefaultCapacity,
    int MaxCapacity,
    int MinimumPlayers)
{
    public EventCreationResult CreateEvent(string name, string format, DateTime startLocal, int capacity, string location)
    {
        var errors = new Dictionary<string, string>();
        if (!Formats.Contains(format))
            errors[nameof(GameEvent.Format)] = "Choose a format offered by this game.";
        if (capacity > MaxCapacity)
            errors[nameof(GameEvent.Capacity)] = $"{Name} events support at most {MaxCapacity} players.";
        else if (capacity < MinimumPlayers)
            errors[nameof(GameEvent.Capacity)] = $"Capacity must allow at least {MinimumPlayers} players.";

        if (errors.Count > 0)
            return new(null, errors);

        return new(new GameEvent(0, name, Id, format, startLocal, DefaultDurationMinutes,
            capacity, MinimumPlayers, location), errors);
    }
}

public sealed record EventCreationResult(GameEvent? Event, IReadOnlyDictionary<string, string> Errors)
{
    public bool IsSuccess => Event is not null;
}
