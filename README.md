# Tabletop Event Calendar

[![CI](https://github.com/kentskinner/tabletop-event-calendar/actions/workflows/ci.yml/badge.svg)](https://github.com/kentskinner/tabletop-event-calendar/actions/workflows/ci.yml)

A small organizer app for scheduling trading card game events, sharing registration links, and tracking available seats.

## Run locally

Requires the .NET 8 SDK.

```bash
dotnet run --project src/EventCalendar
```

Open the URL printed in the terminal. The SQLite database is created automatically. To load three sample events, including a nearly full draft, add the optional seed flag:

```bash
dotnet run --project src/EventCalendar -- --seed
```

Seeding is idempotent: it only adds demo data when the event database is empty. Run the tests with `dotnet test`.

## Design

### Capacity and concurrent registrations

Capacity is stored on the event. It is checked against the selected game template, and the database limits it to `1..30`.

Registration starts a SQLite `IMMEDIATE` transaction, taking the write reservation before it reads the current registrations. Within that transaction, the server compares the registration count with capacity. If space remains, it assigns the first open seat number and inserts the registration. Concurrent requests for the last seat are serialized. The first request commits the seat, and the second sees that the event is full. A unique constraint on `(event_id, seat_number)` is a backstop against duplicate seat assignments. Tests cover both concurrent registration and reuse of a seat after a registration is removed.

If SQLite is locked long enough to exceed the configured timeout, the registration page asks the player to try again instead of returning a server error. The same retry response is used if the seat-number constraint detects a collision.

Player names are unique per event using a case-insensitive database constraint. Duplicate names are rejected within an event. Supporting different players with the same name would require real player identities.

### Game templates

`GameTemplateCatalog` contains templates keyed by stable IDs. Each template provides a display name, formats, default duration, default capacity, maximum capacity, and minimum player count. The creation page uses these values for its options and defaults. `GameTemplate.CreateEvent` validates the format and capacity and supplies the template-owned duration and minimum player count. The creation page and demo seeder both use this path. Events store a copy of the selected values so that changing a template does not alter existing events.

Adding a fourth game requires one new `GameTemplate` entry. The event creation and display code does not branch on game names. A non-card game could use the same structure if formats, duration, and player limits are enough to describe it. If a game needed teams or table assignments, I would add those as template capabilities instead of checking for a specific game name.

### Scope choices and next steps

I spent the timebox on the main workflow: create an event, find it on the calendar, download an ICS invite, open the QR registration link, and register until the event is full. SQLite keeps local setup to one command. I used Ical.Net and QRCoder instead of implementing either format myself.

I chose Razor Pages so the three-hour exercise could stay focused on the server behavior and end-to-end flow. A separate React application and API would add another toolchain and client-state layer without helping the core requirements. I would consider that split if the UI became more interactive or needed to be deployed independently.

I left out authentication, editing and cancellation, recurring events, payments, email, waitlists, and a month-grid calendar. The UI treats entered times as the store's local time. Next I would store an explicit IANA time zone and UTC instant, add database migrations and structured logging, protect organizer actions, and add browser tests for the full workflow. I would also split the read and write services once the application grew beyond this small feature set.

## AI usage

I used OpenAI Codex for the initial scaffolding, CSS, tests, and code review. The first version called a SQLite transaction API that does not exist. The compiler caught it, and I changed the code to `BeginTransaction(deferred: false)` so last-seat registrations are serialized. During browser testing I also fixed incorrect Razor routes and a form bug that reset the selected format after validation.
