namespace EventCalendar.Models;

public sealed record GameTemplate(
    string Id,
    string Name,
    IReadOnlyList<string> Formats,
    int DefaultDurationMinutes,
    int DefaultCapacity,
    int MaxCapacity,
    int MinimumPlayers);
