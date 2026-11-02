# Resource booking

A web app where people book shared resources (meeting rooms, desks, a pool car, equipment) and admins set the rules and see how the space is used. It is modelled on Resource Central, with a generic core: "meeting room" is data an admin enters, never a word in the code. The demo data is a London office.

- **Live app:** https://resource-booking-webapp.onrender.com (free tier: the first load can take a minute)
- **Walkthrough video:** https://youtu.be/rUG5PE8Iqm0
- **Terms:** [`GLOSSARY.md`](GLOSSARY.md) defines every word used in the code, UI and these docs.

## Try it

Pick a user on the sign-in page. There are no passwords in the demo.

| User | Role | Try this |
| --- | --- | --- |
| Alice Member | Member | Office today, then click a gap on Room A and book it. Order coffee with a Boardroom booking (sandwiches need a day's notice). |
| Bob Member | Member | He holds Room A 10:00 to 11:00 on the next weekday: sign in as Alice and try to book over him. He is also waiting for approval on the pool car. |
| Ada Admin | Admin | Approvals (Bob wants the car), Catalogue then Rules, Insights, and Reset demo data. |

"Reset demo data" restores the seed: users, the catalogue, rules, add-ons and about five weeks of bookings generated around today.

## What it does

- **Catalogue:** locations (with an IANA time zone), resource types and resources with an optional capacity. Resources are deactivated, never deleted.
- **Bookings:** create, reschedule and cancel. Double bookings are impossible: a Postgres exclusion constraint guards every slot-holding booking, and the app checks first so it can say who holds the slot.
- **Rules:** always-on checks (valid time range, capacity) plus admin rules per resource type, with per-resource overrides: opening hours, maximum duration, booking window, a daily quota per person and check-in. Every violation is returned at once, in plain English.
- **Alternatives:** a rejected booking comes with the nearest free times on the same resource and other free resources of the same type, as one-click buttons.
- **The office today:** Home shows every bookable space's day, stacked like a building directory. Click a gap to book from that time.
- **Day timeline:** each resource's day is a bar with closed hours hatched and a "now" line. It opens on the first free hour; click a free spot to move the booking there.
- **Add-ons:** catering and similar services are ordered *with* a booking. Each has a lead time and serving hours, checked in local time. Cancelling a booking cancels its orders.
- **Approvals:** some resources (the pool car) need their approver to say yes. A request holds the slot while it waits and is rejected automatically if nobody decides before it starts. The requester hears through in-app notifications.
- **Check-in:** resources can require check-in. No-shows are released automatically so others can book the space.
- **Natural-language search:** "a room for 6 tomorrow at 2" fills in the search form. The AI only interprets; the rules decide. It is off unless an OpenAI key is configured.
- **Insights:** utilisation (booked hours over open hours, per local day so clock changes don't skew it), checked-in and no-show hours, and a filterable booking history.

## How double bookings are prevented

1. **The app checks first.** `BookingJudge` runs every check and rule and looks for overlapping bookings, so a rejection can name who holds the slot and offer alternatives.
2. **Postgres has the final word.** An exclusion constraint (`btree_gist`, `tstzrange(start, end, '[)')`) refuses two overlapping bookings on one resource while either is `Pending`, `Confirmed` or `CheckedIn`. If two requests race past the app's check, the database rejects the loser and the API returns `409 SLOT_TAKEN`.
3. **Retries are safe.** `POST /api/bookings` takes an `Idempotency-Key`, edits carry a `version`, and status changes are compare-and-set (`UPDATE ... WHERE status IN (...)`), so doing one twice is harmless.

An integration test fires simultaneous requests at one slot and checks that exactly one wins.

## Run it locally

Needs the .NET 10 SDK, Node 24 and Docker.

```bash
docker compose up -d --wait               # Postgres 18 on localhost:5432 (dev database "booking")
dotnet run --project src/Booking.Api --launch-profile http   # API on http://localhost:5122; migrates and seeds on start
cd web && npm install && npm run dev      # React app; proxies /api to the API
```

Tests (the integration tests create `booking_test` on the same Postgres):

```bash
dotnet build
dotnet test
```

Everything as one container on http://localhost:8080:

```bash
docker build -t resource-booking .
docker run --rm -p 8080:8080 \
  -e "ConnectionStrings__Default=Host=host.docker.internal;Database=booking;Username=postgres;Password=postgres" \
  resource-booking
```

Add a migration with `dotnet tool restore`, then `dotnet ef migrations add <Name> --project src/Booking.Api --output-dir Infrastructure/Migrations`.

## Configuration

| Setting | Purpose | Default |
| --- | --- | --- |
| `ConnectionStrings__Default` | Postgres connection string for the API | Local Postgres in `appsettings.json` |
| `OpenAI__ApiKey` | Switches on natural-language search | Not set (feature hidden) |
| `OpenAI__Model` | The model that reads the sentence | `gpt-6-luna` |
| `BOOKING_TEST_CONNECTION` | Connection string for the integration-test database | `booking_test` on local Postgres |
| `PORT` | Port the API listens on | 8080 in the container |

Never commit secrets. Put local ones in `src/Booking.Api/appsettings.Development.json` (git-ignored) or environment variables.

**Deploying to Render:** `render.yaml` points Render at the `Dockerfile`. In the dashboard, set `ConnectionStrings__Default` to the Neon connection string in Npgsql format (`Host=...;Database=...;Username=...;Password=...;SSL Mode=Require`) and, optionally, `OpenAI__ApiKey`. Render supplies `PORT`.

## Architecture

```
React (Vite, fetch)  ->  Controllers (HTTP only)  ->  Managers (one use case per method)
                                                        |-> BookingJudge -> RuleEngine -> checks and rules (pure classes)
                                                        |-> AppDbContext (EF Core) -> PostgreSQL
                                                        |-> IClock, ICurrentUser, INotifier, IBookingIntentParser
Background jobs (every minute): complete ended bookings, release no-shows, expire approvals
```

- **Controllers** bind input, authorise and call one manager method.
- **`BookingManager`** is the only code that writes bookings.
- **Rules** are small pure classes with no database access. They return violations rather than throwing, and each has a just-inside and a just-outside unit test.
- **`Program.cs`** is the only place that picks implementations.
- **Time** is stored in UTC and judged in the location's time zone. Tests use a fake clock.
- One Docker container serves the API and the built React app on Render, with Postgres on Neon.

Deliberately left out: repository layers, an interface per manager, MediatR and AutoMapper. DTOs are plain records mapped by hand.

```
src/Booking.Api/
  Features/        Bookings (incl. approvals, check-in), Catalog, Rules, AddOns, Insights, ...: controller + manager + DTOs
  Rules/           checks, rules and the RuleEngine, one file each
  Domain/          entities, enums, TimeRange, Violation
  Infrastructure/  AppDbContext, migrations, demo data, OpenAI parser
  Jobs/            the background jobs
tests/             Booking.UnitTests (rules, maths) and Booking.IntegrationTests (real Postgres)
web/src/features/  one folder per page
```

## Data model

| Table | Key columns |
| --- | --- |
| Users | name, email (unique, lower case), role `Admin` or `Member` |
| Locations | name, IANA time zone |
| ResourceTypes | name |
| Resources | location, type, name, capacity (optional), active, approver (optional user) |
| RuleSettings | resource type **or** resource, kind, settings (`jsonb`); one per kind per target |
| Bookings | resource, owner, start and end (`timestamptz`, UTC, half-open), status, attendees, version, idempotency key, checked-in time, rejection reason |
| AddOns | location, name, lead time, available from and until (local), fulfiller (optional user), active |
| AddOnOrders | booking, add-on, quantity, status |
| Notifications | user, message, created, read |

`Pending`, `Confirmed` and `CheckedIn` hold the slot; `Completed`, `Cancelled`, `Rejected` and `Released` free it. See the glossary for how a booking moves between them.

## API

Errors are ProblemDetails with a `code`: `VALIDATION_FAILED` (400), `SLOT_TAKEN`, `STALE_VERSION` and `INVALID_STATE` (409), and `RULE_VIOLATION` (422) with `violations` and `alternatives`. Lists are paginated (`page`, `pageSize`) in a stable order.

| Method and path | Who | Notes |
| --- | --- | --- |
| `GET /api/session/users`, `POST /api/session`, `DELETE /api/session`, `GET /api/me` | anyone | The demo user picker issues a normal auth cookie |
| `GET/POST/PUT /api/users` | admin | |
| `GET /api/locations`, `/api/resource-types`, `/api/resources` | signed in | Admins can also POST, PUT and DELETE (deactivate) |
| `GET/PUT /api/resource-types/{id}/rules`, `/api/resources/{id}/rules` | GET signed in, PUT admin | A resource's rules show `inherited` or `overridden` |
| `POST /api/bookings` | signed in | `Idempotency-Key` header; 201, or 200 on replay; admins may send `ownerId` |
| `GET /api/bookings?mine=true&when=upcoming\|past`, `GET /api/bookings/{id}` | owner or admin | Upcoming soonest first, past newest first |
| `PUT /api/bookings/{id}` | owner or admin | Reschedule; needs `version`, else 409 `STALE_VERSION` |
| `POST /api/bookings/{id}/cancel` | owner or admin | Idempotent; cancels the booking's add-on orders too |
| `POST /api/bookings/{id}/check-in` | owner or admin | Inside the check-in window; idempotent |
| `GET /api/bookings/approvals`, `POST /api/bookings/{id}/approve`, `POST /api/bookings/{id}/reject` | the resource's approver | Compare-and-set from `Pending`; reject needs a `reason` |
| `GET /api/resources/{id}/bookings?from&to` | signed in | One resource's day |
| `GET /api/availability?locationId&resourceTypeId&start&end&attendees` | signed in | Free resources that pass every rule |
| `GET /api/add-ons`, `POST/PUT /api/add-ons` | GET signed in, writes admin | Ordered with `addOns: [{ addOnId, quantity }]` on create and reschedule |
| `GET /api/booking-intents/enabled`, `POST /api/booking-intents` | signed in | Returns search fields from a sentence; never books |
| `GET /api/notifications`, `GET /api/notifications/summary`, `POST /api/notifications/read-all` | signed in | |
| `GET /api/insights/utilisation?from&to`, `GET /api/insights/history` | admin | |
| `POST /api/demo/reset` | admin | |
| `GET /api/health` | anyone | Reports whether the database is reachable |

## Using the OpenAI API

- One sentence goes to Chat Completions with a **strict JSON schema**, so the reply is always the expected shape.
- The server **validates the reply** again (a known resource type, a date within a year, start before end, sensible attendees) before the user sees it.
- The key and model come from configuration; the key is never committed.
- Without a key the feature is hidden and the normal search form works as before. Tests use a fake parser.

## Design

The look borrows from office wayfinding: room door plates and directory boards, which are made to be read at a glance. There is one hand-written stylesheet (`web/src/styles.css`, no UI library) with a whiteboard and ink palette. Signal yellow marks only "now", your selection and the page you're on. Each kind of space has its own colour, and the type is Atkinson Hyperlegible, designed by the Braille Institute for legibility.

## Decisions and trade-offs

- **A user picker instead of passwords:** it issues a standard cookie with role claims, so real sign-in replaces only that page. A cookie is rejected if its user is deleted or their role changes. Sessions end when the container restarts, because the cookie keys aren't stored; pick a user again.
- **Rules as JSON settings:** one table for every kind, and a typed record per kind validates it. Adding a kind is one class and one record.
- **Approvers and fulfillers per resource and add-on, not roles:** the person who approves the car isn't an admin.
- **Pending requests expire at the start time:** a pending request can't hold a slot past the moment it was for.
- **Utilisation:** booked hours over open hours, per local day. Checked-in hours sit beside booked hours; the gap is the no-show waste.

## Identity and calendars (Microsoft 365)

Resource Central lives inside Microsoft 365. Here:

- **Sign-in:** Entra ID (OpenID Connect) would replace only the user picker. Users would be matched on the stable `oid` claim, while roles, approvers and fulfillers stay in this database.
- **Calendars:** a calendar sync on Microsoft Graph would map each resource to an Exchange room mailbox and mirror bookings as events.

## Roadmap

A fulfiller queue for the catering team (see Future updates in `plans/build-plan.md`), email notifications (a second `INotifier`), room display pages, visitor pre-registration and recurring bookings.
