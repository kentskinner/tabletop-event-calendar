namespace EventCalendar.Models;

public sealed record GameEvent(
    long Id,
    string Name,
    string GameTemplateId,
    string Format,
    DateTime StartLocal,
    int DurationMinutes,
    int Capacity,
    int MinimumPlayers,
    string Location,
    int RegistrationCount = 0)
{
    public DateTime EndLocal => StartLocal.AddMinutes(DurationMinutes);
    public int SeatsRemaining => Math.Max(0, Capacity - RegistrationCount);
    public bool IsFull => RegistrationCount >= Capacity;
}

public sealed record Registration(long Id, long EventId, string PlayerName, int SeatNumber);

public enum RegistrationStatus { Confirmed, Full, Duplicate, EventNotFound }

public sealed record RegistrationResult(RegistrationStatus Status, int? SeatNumber = null);
