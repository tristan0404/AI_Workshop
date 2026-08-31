# Attend.dotnet

Attend.dotnet is a role-based attendance register built with ASP.NET Core Razor Pages, Identity, Entity Framework Core, and SQLite. It combines secure QR check-in, spreadsheet migration, accountable corrections, and role-specific analytics in a responsive accounting-style interface.

## Current status

Phases 1–7 are implemented. The application includes authentication, academic structure, live attendance, historical imports, attendance queries, reporting dashboards, automated quality checks, and production hardening.

## Quick start

```bash
dotnet restore
dotnet ef database update
dotnet run
```

Open the HTTPS address printed by `dotnet run`. When testing a QR code on a phone, both devices must be on the same network and the QR URL must use the computer's reachable network address rather than `localhost`.

The Development environment creates these demonstration accounts:

| Role | Email | Password |
| --- | --- | --- |
| Lecturer | `lecturer@attendly.local` | `Lecturer123!` |
| Student | `student@attendly.local` | `Student123!` |

Demo seeding is disabled in the base configuration and enabled only by `appsettings.Development.json`. Do not use these credentials in a deployed environment.

## Role workflows

### Lecturer

1. Sign in and create or open a course.
2. Enrol students and schedule lecture sessions.
3. Open a session to display its rotating QR code and six-digit fallback code.
4. Monitor the live register, mark exceptions manually, and close attendance.
5. Import historical `.csv` or `.xlsx` matrices through preview and confirmation.
6. Review student queries, approve or reject corrections, and inspect the audit history.
7. Use the dashboard to compare sessions, courses, trends, status distribution, and at-risk attendance.
8. Publish and manage weekly office hours from Overview so enrolled students can see upcoming availability.

### Student

1. Register with a unique student number or activate a record provisioned by an import.
2. Scan the current QR code or enter the lecturer's fallback code.
3. Review attendance history and filter it by course.
4. Query a specific lecture, request a correction, optionally attach evidence, and track the outcome.
5. Use the dashboard to monitor personal attendance and risk indicators.
6. See upcoming office hours offered by lecturers teaching your enrolled courses.

## Innovative features

- Short-lived cryptographically protected QR tokens that rotate during a live session.
- Encrypted six-digit fallback codes for camera or connectivity problems.
- Idempotent check-in and a database uniqueness constraint to prevent duplicate attendance.
- Automatic Present/Late classification from a configurable threshold.
- Transactional spreadsheet preview and import with row-level validation.
- Automatic student provisioning and course enrolment from historical registers.
- Evidence-backed attendance disputes with an immutable lecturer correction trail.
- Role-specific dashboards built with dependency-free, accessible SVG and CSS charts.
- At-risk indicators and a twelve-month consistency heatmap for early intervention.
- Recurring lecturer office hours with overlap validation, ownership protection, and student visibility.

## Architecture and maintainability

The project uses Razor Pages while preserving MVC separation:

- `Models` contains identity, academic, attendance, import, query, and audit entities.
- `.cshtml` pages are views and contain presentation markup only.
- PageModel classes coordinate requests, authorization context, validation, and responses.
- `Services` contains attendance, QR-token, spreadsheet, import, correction, time-zone, and seeding rules.
- `Data` contains EF Core configuration and migrations.
- `Configuration` contains strongly typed, startup-validated settings rather than hard-coded operational values.
- `wwwroot` contains the shared design system, images, and focused client scripts.

Lecturer and student folders are protected by role conventions. Queries additionally filter by course ownership or enrolment so guessing an identifier does not expose another user's resources. State-changing Razor Page handlers use the framework's anti-forgery protection.

## Resilience and validation

- Upload type, size, row count, header, date, duplicate, ambiguity, and attendance-value checks.
- Database transactions for import confirmation and attendance corrections.
- Friendly validation, empty, expired-token, closed-session, and error states.
- Production exception handling, HSTS, HTTP-only authentication cookies, lockout, and security response headers.
- Startup validation for attendance timing, QR lifetime, import limits, and reporting thresholds.
- Fixed-time comparison for fallback attendance codes.

## Automated quality checks

Run the dependency-free quality suite with:

```bash
dotnet run --project Attendly.QualityTests/Attendly.QualityTests.csproj
```

The suite verifies:

- QR token round-trip and tamper rejection.
- Institution time-zone conversion.
- Present and Late classification.
- Duplicate and unenrolled check-in rejection.
- Quoted and semicolon-delimited CSV parsing.
- Duplicate attendance-query prevention and approval audit logging.
- Rejection of invalid configuration ranges.
- Office-hours recurrence, overlap prevention, and ownership-safe deletion.

Before delivery, run:

```bash
dotnet build --no-restore
dotnet run --project Attendly.QualityTests/Attendly.QualityTests.csproj --no-restore
git diff --check
```

## Manual acceptance checklist

- Sign in with both demonstration roles and confirm the other role's pages return Access Denied.
- Create, edit, archive, and open a lecturer-owned course.
- Add/remove an enrolment and create/cancel a lecture session.
- Open attendance, scan the QR code, retry the same check-in, use the fallback code, and close the window.
- Confirm invalid, expired, closed, and unenrolled check-ins show useful messages.
- Preview a valid and invalid import; confirm only the valid import changes data.
- Submit, review, approve, and reject attendance queries; inspect the audit trail.
- Check dashboards with populated and empty data at desktop and mobile widths.
- Navigate interactive controls by keyboard and verify visible focus and readable chart fallbacks.

## Import format

Lecturers can import comma- or semicolon-separated `.csv` files and `.xlsx` attendance matrices from **Imports**. The required columns are `Student Name`, `Student Number` (or `Student No`), followed by date columns (`YYYY-MM-DD` or `YYYY/MM/DD`). Values are `1` (present) or `0` (absent). Every upload is validated and previewed before a single transactional confirmation.

```csv
Student Name,Student Number,2026-08-24,2026-08-31
Thabo Mokoena,STU-2026-001,1,0
Lerato Dlamini,STU-2026-002,1,1
```

Confirmation automatically enrolls existing student accounts into the selected course. Students not yet in the system receive provisioned, passwordless accounts linked to their student numbers; they activate those records through normal registration instead of creating duplicate profiles.

The Razor Pages project follows MVC separation: entities and import view models are in `Models`, `.cshtml` files are views, PageModels perform controller/request coordination, and parsing/validation/business rules are isolated in `Services`.

## Query and correction workflow

Students can query any past lecture, including one with no recorded check-in, request a corrected status, attach PDF/JPG/PNG evidence, and track the decision. Lecturers have an owned-course review queue, approve/reject workflow, general manual editing, and an immutable change log recording the old status, new status, lecturer, time, and reason.

## Analytics dashboards

Role-specific dashboards calculate attendance KPIs, trends, lecture/course comparisons, status distribution, risk counts, and a 12-month consistency heatmap from EF Core data. The charts are dependency-free native SVG/CSS components with keyboard-readable data, accessible fallbacks, responsive layouts, tooltips, and reduced-motion support.

## Feature history

### Implemented foundation

- Individual account authentication
- Student and Lecturer roles
- Student-only public registration (users cannot grant themselves lecturer access)
- Unique student numbers
- Seeded development accounts
- Role-protected Razor Pages folders ready for later phases
- Responsive accounting-style application shell
- Initial Identity database migration

### Phase 2 academic workspace

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

### Public experience

- Public landing page at `/`
- Authenticated role-aware overview at `/Dashboard`
- Responsive marketing navigation, hero, workflow, role, and call-to-action sections
- Optimized student, lecturer, and QR artwork stored locally under `wwwroot/images/landing`
- Authentication calls to action return users to the protected dashboard

### Phase 3 attendance capture

- Lecturer-controlled attendance windows with automatic expiry
- Rotating, short-lived QR tokens protected by ASP.NET Core Data Protection
- Six-digit fallback codes encrypted at rest
- Authenticated student confirmation before recording attendance
- Present and late classification using configurable thresholds
- Duplicate-safe, idempotent check-ins backed by a unique database constraint
- Live lecturer progress, roster polling, countdown, and manual present fallback
- Student attendance history with course filtering and capture-source details

### Demonstration data

The local migrated database includes `INF201 · Information Systems`, the seeded student enrolment, and one scheduled lecture so both role experiences can be reviewed immediately.
