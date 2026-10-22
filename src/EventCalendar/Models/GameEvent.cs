namespace EventCalendar.Models;

public sealed record GameEvent
{
    internal GameEvent(long id, string name, string gameTemplateId, string format, DateTime startLocal,
        int durationMinutes, int capacity, int minimumPlayers, string location, int registrationCount = 0)
    {
        Id = id;
        Name = name;
        GameTemplateId = gameTemplateId;
        Format = format;
        StartLocal = startLocal;
        DurationMinutes = durationMinutes;
        Capacity = capacity;
        MinimumPlayers = minimumPlayers;
        Location = location;
        RegistrationCount = registrationCount;
    }

    public long Id { get; }
    public string Name { get; }
    public string GameTemplateId { get; }
    public string Format { get; }
    public DateTime StartLocal { get; }
    public int DurationMinutes { get; }
    public int Capacity { get; }
    public int MinimumPlayers { get; }
    public string Location { get; }
    public int RegistrationCount { get; }
    public DateTime EndLocal => StartLocal.AddMinutes(DurationMinutes);
    public int SeatsRemaining => Math.Max(0, Capacity - RegistrationCount);
    public bool IsFull => RegistrationCount >= Capacity;
}

public enum RegistrationStatus { Confirmed, Full, Duplicate, InvalidName, Busy, EventNotFound }

public sealed record RegistrationResult(RegistrationStatus Status, int? SeatNumber = null);
