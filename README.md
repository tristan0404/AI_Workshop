# Attendly

Attendly is a role-based attendance register built with ASP.NET Core Razor Pages, Identity, EF Core, and SQLite.

## Phase 1 foundation

- Individual account authentication
- Student and Lecturer roles
- Student-only public registration (users cannot grant themselves lecturer access)
- Unique student numbers
- Seeded development accounts
- Role-protected Razor Pages folders ready for later phases
- Responsive accounting-style application shell
- Initial Identity database migration

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

## Next phase

Phase 2 adds courses, lecturer assignments, enrolments, and lecture sessions. The dashboard deliberately displays zero/empty states until those records exist.
