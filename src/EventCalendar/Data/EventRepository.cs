using System.Globalization;
using EventCalendar.Models;
using Microsoft.Data.Sqlite;

namespace EventCalendar.Data;

public sealed class EventRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("Events")
        ?? throw new InvalidOperationException("Missing Events connection string.");

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                game_template_id TEXT NOT NULL,
                format TEXT NOT NULL,
                start_local TEXT NOT NULL,
                duration_minutes INTEGER NOT NULL CHECK(duration_minutes > 0),
                capacity INTEGER NOT NULL CHECK(capacity BETWEEN 1 AND 30),
                minimum_players INTEGER NOT NULL CHECK(minimum_players > 0 AND minimum_players <= capacity),
                location TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS registrations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                event_id INTEGER NOT NULL REFERENCES events(id) ON DELETE CASCADE,
                player_name TEXT NOT NULL COLLATE NOCASE,
                seat_number INTEGER NOT NULL CHECK(seat_number > 0),
                created_utc TEXT NOT NULL,
                UNIQUE(event_id, player_name),
                UNIQUE(event_id, seat_number)
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<long> CreateEventAsync(GameEvent gameEvent)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO events (name, game_template_id, format, start_local, duration_minutes, capacity, minimum_players, location)
            VALUES ($name, $game, $format, $start, $duration, $capacity, $minimum, $location);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$name", gameEvent.Name);
        command.Parameters.AddWithValue("$game", gameEvent.GameTemplateId);
        command.Parameters.AddWithValue("$format", gameEvent.Format);
        command.Parameters.AddWithValue("$start", gameEvent.StartLocal.ToString("O"));
        command.Parameters.AddWithValue("$duration", gameEvent.DurationMinutes);
        command.Parameters.AddWithValue("$capacity", gameEvent.Capacity);
        command.Parameters.AddWithValue("$minimum", gameEvent.MinimumPlayers);
        command.Parameters.AddWithValue("$location", gameEvent.Location);
        return (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Event was not created."));
    }

    public async Task<IReadOnlyList<GameEvent>> ListEventsAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.id, e.name, e.game_template_id, e.format, e.start_local, e.duration_minutes,
                   e.capacity, e.minimum_players, e.location, COUNT(r.id)
            FROM events e LEFT JOIN registrations r ON r.event_id = e.id
            GROUP BY e.id ORDER BY e.start_local;
            """;
        return await ReadEventsAsync(command);
    }

    public async Task<GameEvent?> GetEventAsync(long id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.id, e.name, e.game_template_id, e.format, e.start_local, e.duration_minutes,
                   e.capacity, e.minimum_players, e.location, COUNT(r.id)
            FROM events e LEFT JOIN registrations r ON r.event_id = e.id
            WHERE e.id = $id GROUP BY e.id;
            """;
        command.Parameters.AddWithValue("$id", id);
        return (await ReadEventsAsync(command)).SingleOrDefault();
    }

    public async Task<RegistrationResult> RegisterAsync(long eventId, string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return new(RegistrationStatus.InvalidName);

        try
        {
            return await RegisterCoreAsync(eventId, playerName.Trim());
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6)
        {
            return new(RegistrationStatus.Busy);
        }
    }

    private async Task<RegistrationResult> RegisterCoreAsync(long eventId, string playerName)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        // deferred: false maps to BEGIN IMMEDIATE, reserving the writer before we count seats.
        await using var transaction = connection.BeginTransaction(deferred: false);

        var eventCommand = connection.CreateCommand();
        eventCommand.Transaction = transaction;
        eventCommand.CommandText = "SELECT capacity FROM events WHERE id = $id";
        eventCommand.Parameters.AddWithValue("$id", eventId);
        var capacityValue = await eventCommand.ExecuteScalarAsync();
        if (capacityValue is null)
        {
            await transaction.RollbackAsync();
            return new(RegistrationStatus.EventNotFound);
        }

        var capacity = Convert.ToInt32(capacityValue);
        var occupancyCommand = connection.CreateCommand();
        occupancyCommand.Transaction = transaction;
        occupancyCommand.CommandText = "SELECT COUNT(*) FROM registrations WHERE event_id = $id";
        occupancyCommand.Parameters.AddWithValue("$id", eventId);
        var occupancy = Convert.ToInt32(await occupancyCommand.ExecuteScalarAsync());
        if (occupancy >= capacity)
        {
            await transaction.RollbackAsync();
            return new(RegistrationStatus.Full);
        }

        var seatCommand = connection.CreateCommand();
        seatCommand.Transaction = transaction;
        seatCommand.CommandText = """
            WITH RECURSIVE seats(number) AS (
                SELECT 1
                UNION ALL
                SELECT number + 1 FROM seats WHERE number < $capacity
            )
            SELECT number FROM seats
            WHERE NOT EXISTS (
                SELECT 1 FROM registrations
                WHERE event_id = $id AND seat_number = seats.number
            )
            ORDER BY number
            LIMIT 1;
            """;
        seatCommand.Parameters.AddWithValue("$id", eventId);
        seatCommand.Parameters.AddWithValue("$capacity", capacity);
        var seat = Convert.ToInt32(await seatCommand.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("No seat was available below capacity."));

        try
        {
            var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO registrations (event_id, player_name, seat_number, created_utc)
                VALUES ($event, $name, $seat, $created);
                """;
            insert.Parameters.AddWithValue("$event", eventId);
            insert.Parameters.AddWithValue("$name", playerName);
            insert.Parameters.AddWithValue("$seat", seat);
            insert.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
            await insert.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            return new(RegistrationStatus.Confirmed, seat);
        }
        catch (SqliteException ex) when (IsUniqueViolation(ex, "registrations.player_name"))
        {
            await transaction.RollbackAsync();
            return new(RegistrationStatus.Duplicate);
        }
        catch (SqliteException ex) when (IsUniqueViolation(ex, "registrations.seat_number"))
        {
            await transaction.RollbackAsync();
            return new(RegistrationStatus.Busy);
        }
    }

    private static bool IsUniqueViolation(SqliteException exception, string column) =>
        exception.SqliteErrorCode == 19
        && exception.SqliteExtendedErrorCode == 2067
        && exception.Message.Contains(column, StringComparison.Ordinal);

    private static async Task<IReadOnlyList<GameEvent>> ReadEventsAsync(SqliteCommand command)
    {
        var events = new List<GameEvent>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            events.Add(new(
                reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.GetInt32(5), reader.GetInt32(6),
                reader.GetInt32(7), reader.GetString(8), reader.GetInt32(9)));
        }
        return events;
    }
}
