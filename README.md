# Riva - Meeting Room Booking

A small ASP.NET Core 8 application with a plain JavaScript frontend and persistent SQLite database. No login, cloud account, containers, or paid services are required.

## Run locally (Windows)

Install the .NET 8 SDK. Extract this project into a local folder and open PowerShell in the folder containing `RoomBooking.sln`.

```powershell
dotnet restore
dotnet run --project src/RoomBooking
```

Open `RoomBooking.sln` in Visual Studio 2022 with the ASP.NET workload, select RoomBooking as the startup project, and run it.

SQLite creates `bookings.db` automatically and seeds Room A, Room B, and Room C. No external database setup is needed. Stop the application and remove the database plus its `-wal`/`-shm` companions to reset the local demo. Never remove a live database.

## Run tests

API integration tests use a fresh, real SQLite file for each test, with the production HTTP pipeline and database code. They do not use a mocked database or EF in-memory provider.

```powershell
dotnet test RoomBooking.sln
```

For the browser interaction test, install Node.js 20 or later, then:

```powershell
cd tests/frontend
npm ci
npx playwright install chromium
npm test
```

Playwright starts a separate application on port 5081 with a unique temporary database. Its test creates a booking, verifies conflict feedback, cancels it, and rebooks the released time. Temporary browser-test database files live in the OS temporary directory and may be removed after tests finish.

## Application structure

- `src/RoomBooking/Controllers`: HTTP validation, status codes, and API contracts.
- `src/RoomBooking/Services`: parameterized queries and transactional booking workflow.
- `src/RoomBooking/Data`: SQLite connections, schema, constraints, triggers, and room seed data.
- `src/RoomBooking/Models`: input and output records.
- `src/RoomBooking/wwwroot`: responsive UI, calling only the API for data/actions.
- `tests/RoomBooking.Tests`: API integration tests, including simultaneous requests.
- `tests/frontend`: Playwright browser interaction test.

The application intentionally uses one web project and one C# test project. A generic repository, authentication, and a separate SPA build would add little value to this assessment.

## API

| Method | URL | Result |
| --- | --- | --- |
| GET | `/api/rooms` | Seeded rooms |
| GET | `/api/bookings?day=2026-11-04` | Active bookings on the UTC day |
| POST | `/api/bookings` | 201 booking, 400 invalid input, 404 missing room, or 409 overlap |
| DELETE | `/api/bookings/{id}` | 204 cancelled; 404 if missing or already cancelled |

Example POST JSON:

```json
{
  "roomId": 1,
  "name": "Tanveer",
  "startUtc": "2026-11-04T10:00:00Z",
  "endUtc": "2026-11-04T11:00:00Z"
}
```

Times must have a zero UTC offset. They are stored as integer UTC ticks (100 ns precision), preserving the input precision without floating-point comparison. End must be strictly after start. Both must have the same UTC date; ending at midnight on the following day is deliberately rejected under the assessment's one-calendar-day rule. The UI labels all times UTC and does not convert entered wall times to the browser's local timezone. Past dates are allowed because the brief does not forbid them.

## Preventing double bookings

Overlap uses half-open intervals: `existing.StartUtc < requested.EndUtc && existing.EndUtc > requested.StartUtc`. This permits 10:00–11:00 followed by 11:00–12:00.

Creation begins a SQLite **IMMEDIATE transaction** (`BeginTransaction(deferred: false)`) before checking room existence or availability. SQLite acquires its write reservation at that point. Another writer waits; after the first transaction commits, the waiting writer checks the committed booking and returns 409. The check and insert happen inside the same transaction. Disposal rolls back unsuccessful transactions.

Database INSERT and UPDATE triggers independently reject overlapping active bookings, including writes outside the normal service. Cancellation marks the row inactive atomically; cancelled rows do not participate in overlap checks. The foreign key and CHECK constraints protect basic integrity.

The concurrent integration test releases two overlapping HTTP requests from one gate, asserts exactly one 201 and one 409, and directly verifies only one row was saved. Other tests verify containment, partial overlap, identical intervals, adjacency, separate rooms, invalid dates/times, and cancellation/rebooking.

### Tradeoffs

SQLite serializes writers across the database, including writes for different rooms. Different rooms may hold bookings at the same time, but write throughput is limited. The short transaction and 15-second busy timeout suit a small local application. Lock exhaustion returns 503 with Retry-After rather than falsely reporting a booking conflict. Under normal competing requests, the second writer waits and returns 409 after checking availability.

Database calls are synchronous because Microsoft.Data.Sqlite executes SQLite I/O synchronously. This is acceptable for this small assessment; it is a throughput limitation, not a claim of scalable asynchronous I/O. WAL allows readers alongside a writer. SQLite is intended here for one local shared database file, not distributed replicas or a network filesystem.

Schema initialization is idempotent for a clean installation; it is not a production schema migration system. Cancellation is intentionally non-idempotent at the HTTP status level (a second cancellation returns 404). There is no booking edit or restore endpoint.

## What I would improve next

For a higher-throughput deployment, use PostgreSQL with a range exclusion constraint per room (or equivalent database-level serialization), introduce versioned migrations, add pagination and structured operational telemetry, and extend accessibility and browser coverage. Add authentication/authorization and request idempotency only when product requirements call for them.

## Libraries and AI disclosure

- ASP.NET Core: HTTP hosting, controllers, validation, Problem Details, static files.
- Microsoft.Data.Sqlite: persistent SQLite access with parameterized SQL.
- xUnit, Microsoft.NET.Test.Sdk, Microsoft.AspNetCore.Mvc.Testing: integration testing.
- Playwright: browser interaction testing.
- ChatGPT/Codex assisted with implementation, tests, and documentation. The submitter should review, run, and understand all code, especially the transaction and overlap rules, before submission.

See `VERIFICATION.md` for checks executed in the preparation environment.
