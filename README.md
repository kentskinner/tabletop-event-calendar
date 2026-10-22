# Tabletop Event Calendar

A deliberately small organizer app for scheduling trading-card-game events, sharing QR registration links, and enforcing the last available seat on the server.

## Run locally

Requires the .NET 8 SDK.

```bash
dotnet run --project src/EventCalendar
```

Open the URL printed in the terminal. The SQLite database is created automatically. Run the tests with `dotnet test`.

## Design

### Capacity and concurrent registrations

Capacity is an integer on the event, constrained both by the selected game template and by a database check (`1..30`). A registration owns a numbered seat. The database has a unique constraint on `(event_id, seat_number)`, so the same seat can never be issued twice.

Registration starts a SQLite `IMMEDIATE` transaction, which takes the database write reservation before reading the current registrations. Inside that transaction the server reads the event capacity, computes the next seat, rejects it if it exceeds capacity, and inserts the registration. Concurrent requests for the last seat are serialized: the winner commits that seat; the next request observes the new count and receives a clear `Full` result. Capacity is therefore an API/data-layer invariant, not a disabled button. A concurrency test fires two registrations at a one-seat event and verifies exactly one succeeds.

Player names are unique per event using a case-insensitive database constraint. This is a pragmatic anonymous-registration rule, not an identity system; two real people with the same name would need organizer assistance in a production version.

### Game templates

`GameTemplateCatalog` contains immutable templates keyed by stable IDs. Each template supplies display name, available formats, default duration, default capacity, maximum capacity, and minimum players. The creation page uses those values for its dynamic choices and defaults; the server independently validates the submitted format and capacity against the same template. Event records snapshot the chosen format, duration, capacity, and minimum-player rule, so later template changes do not silently rewrite scheduled events.

Adding a fourth game means adding one `GameTemplate` entry; event creation and display require no conditional game logic. A non-card game can use the same shape if formats, duration, and player bounds describe it. If it needed genuinely different concepts—teams or tables, for example—I would add optional, typed template capabilities rather than branching on the game name.

### Scope choices and next steps

I spent the timebox on the complete path: create an event, find it in a day-grouped calendar, download a standards-based ICS invite, scan or follow a generated QR link, and register until the server says the event is full. SQLite keeps setup to one command and is sufficient for a single organizer. I used Ical.Net and QRCoder rather than hand-writing either format.

I chose server-rendered Razor Pages to keep the three-hour exercise focused on transactional correctness and the complete user flow. A separate React SPA and API would add another toolchain, deployment boundary, and client-state layer without improving the core requirements; I would introduce that split if richer client interactions or independent frontend deployment justified it.

I deliberately cut authentication, editing/cancellation, recurring events, payments, email, waitlists, and a full month-grid library, as requested. The UI treats entered times as the store's local time; production would store an explicit IANA time zone and UTC instant, add migrations and structured logging, protect organizer actions, apply CSRF/rate-limit review, and run browser-level tests for QR/ICS and the whole flow. I would also separate read/write services once the product had more than this single vertical slice.

## AI usage

I used OpenAI Codex to accelerate scaffolding, CSS, test cases, and a first-pass review of the capacity transaction. I did not accept the tempting “count registrations, then insert” approach: those are two independent operations and can oversubscribe the last seat. I replaced it with an immediate database transaction plus unique seat constraints, and kept the database-backed concurrency test that proves the behavior.
