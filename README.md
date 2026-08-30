# Attendly

Attendly is a role-based attendance register built with ASP.NET Core Razor Pages, Identity, EF Core, and SQLite.

## Implemented foundation

- Individual account authentication
- Student and Lecturer roles
- Student-only public registration (users cannot grant themselves lecturer access)
- Unique student numbers
- Seeded development accounts
- Role-protected Razor Pages folders ready for later phases
- Responsive accounting-style application shell
- Initial Identity database migration

## Phase 2 academic workspace

- Lecturer-owned course creation and editing
- Safe course archiving for historical continuity
- Student lookup and enrolment by student number or email
- Duplicate enrolment protection
- Lecture-session scheduling and cancellation
- Institution time-zone conversion with UTC database storage
- Student course and lecture schedule views
- Resource-level filtering so users cannot access another user's courses
- Live dashboard counts for courses, sessions, and students/enrolments
- Responsive course tables, forms, cards, and empty states

## Public experience

- Public landing page at `/`
- Authenticated role-aware overview at `/Dashboard`
- Responsive marketing navigation, hero, workflow, role, and call-to-action sections
- Optimized student, lecturer, and QR artwork stored locally under `wwwroot/images/landing`
- Authentication calls to action return users to the protected dashboard

## Phase 3 attendance capture

- Lecturer-controlled attendance windows with automatic expiry
- Rotating, short-lived QR tokens protected by ASP.NET Core Data Protection
- Six-digit fallback codes encrypted at rest
- Authenticated student confirmation before recording attendance
- Present and late classification using configurable thresholds
- Duplicate-safe, idempotent check-ins backed by a unique database constraint
- Live lecturer progress, roster polling, countdown, and manual present fallback
- Student attendance history with course filtering and capture-source details

## Run locally

```bash
dotnet restore
dotnet ef database update
dotnet run
```

The development environment creates these demo accounts:

| Role | Email | Password |
| --- | --- | --- |
| Lecturer | `lecturer@attendly.local` | `Lecturer123!` |
| Student | `student@attendly.local` | `Student123!` |

Demo seeding is disabled by default and enabled only in `appsettings.Development.json`. Production credentials must use environment variables or a secret store.

## Project structure

- `Areas/Identity` — customized account pages
- `Data` — EF Core context, design-time factory, and migrations
- `Models/Identity` — user and role definitions
- `Pages` — Razor Pages UI
- `Services` — application setup and later business services
- `wwwroot` — the Attendly design system and client scripts

## Demonstration data

The local migrated database includes `INF201 · Information Systems`, the seeded student enrolment, and one scheduled lecture so both role experiences can be reviewed immediately.

## Next phase

Phase 3 adds rotating QR attendance, the fallback code, attendance windows, live check-in, and duplicate check-in protection.
