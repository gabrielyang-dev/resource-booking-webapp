# Build plan

The build is a series of vertical pull requests. Each one is a thin, complete path through every layer it touches (database, API, UI, tests) and can be demoed on the live app by itself.

## Ground rules

- **Product:** a generic booking app for locations, resource types, resources, rules, bookings and add-ons, modelled on Resource Central. Code uses only generic terms. The seed data is an office.
- **Stack:** ASP.NET Core (.NET 10) controllers, EF Core with Npgsql on PostgreSQL (Neon), React with Vite and TypeScript, xUnit with integration tests against a real Postgres. The app ships as one Docker container on Render.
- **Tiers:** P0 is the brief, P1 brings it close to Resource Central, P2 is optional. **All of P0 is built and tested before any P1 work starts, and all of P1 before any P2**, whatever the graph would allow.
- **Every PR:** branch from `main`, one PR, squash-merge, CI green, and a manual check through the real UI or API recorded in the PR description.
- **Doors:** a PR that adds a migration is close to one-way once deployed. The rollback is a forward migration plus a revert of the code. PRs without migrations are two-way (revert the squash commit).

## Dependency graph

`A -> B` means B cannot start until A is merged.

```
P0
01 -> 02 -> 03 -> 04 -> 05
                  04 -> 06 -> 07 -> 08
                  05 -> 06

P1 (starts after 08)
06 -> 09 -> 14
06 -> 10
05 -> 12
05, 08 -> 13
08, 09, 10, 12, 13, 14 -> 15

P2 (starts after 15, each optional)
13 -> 16      05 -> 17      05 -> 18      05 -> 19      08 -> 20

Future updates (after the handover)
10 -> 11
```

Build order: 01 to 08 in sequence, then 09, 10, 12, 13 and 14 (any order the graph allows), then 15, then P2. 11 moved to Future updates.

## P0: the brief

### 01 Walking skeleton
- **Branch / title:** `chore/walking-skeleton` · `chore: add api, web app, ci and deploy`
- **Why:** every later PR should be demoable on the live link, and "CI green" should mean something from day one.
- **Changes:** `Booking.sln` with `src/Booking.Api`, `tests/Booking.UnitTests` and `tests/Booking.IntegrationTests`. `GET /api/health` returns 200 and checks the database connection. A Vite React app in `web/` calls it and shows the status. The API serves the built React app. A multi-stage `Dockerfile`. GitHub Actions runs build and tests with a Postgres service container. The integration-test harness creates a database, migrates it and resets it between tests. The app is deployed to Render with Neon. Adds `GLOSSARY.md` and fills in the README commands.
- **Tests:** one unit test, plus one integration test that hits `/api/health` against real Postgres.
- **Demo:** open the live URL and see "API: healthy, database: connected".
- **Door:** two-way (no schema yet).

### 02 Users, roles and sign-in
- **Branch / title:** `feat/users-and-roles` · `feat: add users with roles and sign-in`
- **Why:** the brief needs admins who add users, and every later rule needs a known current user.
- **Changes:** `User` entity (name, email, role `Admin` or `Member`) and the first migration. Sign-in is a user picker: `GET /api/session/users` lists the users to pick from, `POST /api/session` with a user id issues a standard ASP.NET cookie with id, name and role claims, and `DELETE /api/session` signs out. `GET /api/me`. Controllers use `[Authorize]` and `[Authorize(Roles = "Admin")]`, and `ICurrentUser` reads the claims. Real SSO would replace only the sign-in step. Admin-only `GET/POST/PUT /api/users`. Admin-only `POST /api/demo/reset` restores the seed data (each later PR adds its own data to the seed). A ProblemDetails handler with a `code` extension and the status codes 400, 401, 403 and 404. The pagination pattern (`page`, `pageSize`, stable order by name then id) is set here and reused for every list. Seeds one admin and two members. UI: a sign-in page with the picker, a user menu with "Switch user", an admin Users page, and a "Reset demo data" button.
- **Tests:** integration tests for 401 when signed out, 403 for a member calling an admin endpoint, a role claim matching the stored role, a duplicate email returning 409, reset restoring the seed, and stable pagination.
- **Demo:** pick the admin and add a user. Switch to that user; the Users page is hidden and the API returns 403. Reset demo data and the new user is gone.
- **Door:** one-way (migration).

### 03 Catalogue: locations, resource types, resources
- **Branch / title:** `feat/catalogue` · `feat: add locations, resource types and resources`
- **Why:** there is nothing to book without a catalogue, and every later demo needs realistic data.
- **Changes:** `Location` (name, IANA time zone), `ResourceType` (name, e.g. data "Meeting room"), `Resource` (location, type, name, capacity, active flag). Admin CRUD under `/api/locations`, `/api/resource-types`, `/api/resources`, and member read with filters by location and type. The time zone is validated against IANA. Seed data: one London office, with meeting rooms, desks, a pool car and a projector. UI: admin Catalogue pages, and a member "Find a resource" list.
- **Tests:** integration tests for CRUD, an invalid time zone returning 400, and filters.
- **Demo:** as admin, add a room to the London office. As a member, filter by "Meeting room" and see it.
- **Door:** one-way (migration).

### 04 Create a booking without double bookings
- **Branch / title:** `feat/create-booking` · `feat: create bookings with overlap constraint`
- **Why:** this is the core of the brief: book a resource, and never book the same slot twice.
- **Changes:** `Booking` (resource, owner, start and end as UTC `timestamptz`, status, attendee count, `Version`, created time). `TimeRange` uses half-open `[start, end)`. `BookingStatus` has all seven values, but this PR only produces `Confirmed`. Migration enables `btree_gist` and adds an exclusion constraint on `(resource_id, tstzrange(start, end, '[)'))` filtered to `Pending`, `Confirmed` and `CheckedIn`. `IClock` is introduced here. `BookingManager.Create` checks for overlap first so it can give a friendly `SLOT_TAKEN` (409), and maps `23P01` to the same 409. `POST /api/bookings` with `Idempotency-Key`: a replay returns 200 with the original booking (key stored per user). `GET /api/bookings?mine=true` and `GET /api/resources/{id}/bookings?from&to`. Basic input validation returns 400. UI: a booking form on a resource (date, start, end, attendees), a day view of the resource's bookings, and a "My bookings" page.
- **Tests:** integration tests for overlap returning 409, back-to-back (10:00 end, 10:00 start) succeeding, 20 parallel requests for one slot where exactly one wins, an idempotent replay returning the same booking without a second row, and stored times having `DateTimeKind.Utc`.
- **Demo:** book Room A for 10:00 to 11:00. A second user trying 10:30 to 11:30 sees "Room A is already booked from 10:00 to 11:00". 11:00 to 12:00 works.
- **Door:** one-way (migration, constraint).

### 05 Reschedule and cancel
- **Branch / title:** `feat/reschedule-cancel` · `feat: reschedule and cancel bookings`
- **Why:** plans change, and the brief asks for it. This also sets the patterns for every later status change.
- **Changes:** `PUT /api/bookings/{id}` with `Version`. A stale version returns 409 `STALE_VERSION`. The overlap check excludes the booking itself. `POST /api/bookings/{id}/cancel` is compare-and-set (`UPDATE ... WHERE status IN (...)`): cancelling twice returns 200 with the same result, and cancelling a finished booking returns 409 `INVALID_STATE`. Owners can change their own bookings; admins can change anyone's (owner checks run against the owner, not the admin). Produces `Cancelled`. UI: edit and cancel actions on "My bookings" and the day view.
- **Tests:** integration tests for moving a booking within its own slot, moving onto another booking (409), a stale version (409), cancel run twice, a cancelled slot becoming bookable again, and a member editing someone else's booking (403).
- **Demo:** move a 10:00 booking to 10:30. Open the same booking in two tabs, save both, and the second shows "This booking changed since you opened it. Reload and try again."
- **Door:** two-way (no migration; `Version` was added in 04).

### 06 Rule engine with always-on checks
- **Branch / title:** `feat/rule-engine` · `feat: add rule engine with capacity and time checks`
- **Why:** a single place decides whether a booking is allowed, and it returns every problem at once.
- **Changes:** `Violation` (code, plain-language message), `IBookingCheck`, `RuleEngine` (runs everything and collects all violations). Checks: valid time range (end after start, not in the past, whole minutes) and capacity (attendees no more than the resource's capacity). The 400 validation from 04 that these checks now cover is deleted. `BookingManager` runs the engine on create and reschedule, for the booking's owner. A failure returns 422 `RULE_VIOLATION` with `violations`. UI: violations listed under the form.
- **Tests:** two unit tests per check (just inside, just outside). An integration test where a booking that breaks two checks returns both violations, and a reschedule that re-runs the checks.
- **Demo:** book a 4-person room for 6 people, starting in the past. Both problems are listed together.
- **Door:** two-way.

### 07 Admin-configured rules
- **Branch / title:** `feat/booking-rules` · `feat: add configurable booking rules with overrides`
- **Why:** admins set constraints per resource type, with exceptions for particular resources.
- **Changes:** `RuleSetting` (resource type or resource, `RuleKind`, JSON settings). The effective rule for a kind is the resource override if there is one, otherwise the type default. `IBookingRule` with one class per kind: `OpeningHours` (judged in the location's time zone), `MaxDuration`, `BookingWindow` (minimum notice and maximum days ahead), `DailyQuota` (hours per owner per local day). `GET/PUT /api/resource-types/{id}/rules` and `/api/resources/{id}/rules`. Seeds sensible office defaults. UI: a Rules tab on resource types and resources showing "inherited" or "overridden".
- **Tests:** two unit tests per rule, including a time-zone edge case for opening hours (UTC 07:30 is 08:30 in London in summer). Integration tests for an override beating the default, and an admin booking for a member being judged by the member's quota.
- **Demo:** set meeting rooms to 08:00 to 18:00 and a 2-hour maximum. A 17:00 to 19:30 booking is rejected with both reasons. Override one room to 4 hours and it accepts 3 hours.
- **Door:** one-way (migration).

### 08 Utilisation and history
- **Branch / title:** `feat/insights` · `feat: add utilisation and booking history`
- **Why:** admins need to see how resources are used, which completes the brief.
- **Changes:** a background job (`Jobs/CompleteEndedBookings`) moves `Confirmed` and `CheckedIn` bookings past their end to `Completed` (compare-and-set, so it is safe to run twice). `GET /api/insights/utilisation?from&to&locationId&resourceTypeId` returns booked hours divided by open hours, per resource and per type. Booked hours count `Confirmed`, `CheckedIn` and `Completed` bookings, clipped to the range. Open hours come from the effective opening-hours rule, or 24 hours a day if there is none, and are calculated per local day in the location's time zone. `GET /api/insights/history` is a paginated list of bookings with any status, filterable by resource, user and date, newest first then id. UI: an admin Insights page with a utilisation table and bar, and a history table.
- **Tests:** unit tests for the utilisation maths: a booking clipped at each end of the range, `Pending` and `Cancelled` bookings excluded, and the open hours of a 24-hour resource on the day the clocks go forward (23) and back (25), while an 08:00 to 18:00 room stays at 10. Integration tests for history filters and pagination, and the completion job run twice.
- **Demo:** with seeded past bookings, Insights shows Room A at 62% this week. Filtering history by a user lists their cancelled and completed bookings.
- **Door:** two-way (no migration).

## P1: Resource Central parity

### 09 Explained rejections with alternatives
- **Branch / title:** `feat/alternatives` · `feat: suggest alternatives when a booking is rejected`
- **Why:** a rejection should never be a dead end.
- **Changes:** `GET /api/availability?locationId&resourceTypeId&start&end&attendees` returns free resources that pass all rules. On `SLOT_TAKEN` or `RULE_VIOLATION`, the response adds `alternatives`: the nearest valid times on the same resource (same duration, searched in 15-minute steps within the day) and other resources of the same type at the same location for the same time, limited to 3 of each. UI: alternatives shown as buttons that fill the form; "Find a resource" gains a time search.
- **Tests:** unit tests for the alternative search. An integration test where a taken slot returns the next free slot and a free sibling room.
- **Demo:** try a taken 10:00 slot and click "Room B, 10:00 to 11:00" to book it.
- **Door:** two-way.

### 10 Add-ons ordered with a booking
- **Branch / title:** `feat/add-ons` · `feat: order add-ons with a booking`
- **Why:** catering and AV are ordered for a booking. They are not booked as resources themselves.
- **Changes:** `AddOn` (location, name, lead time, available hours, fulfiller user) and `AddOnOrder` (booking, add-on, quantity, status `Requested`, `Delivered`, `Cancelled`). Always-on checks for lead time and available hours (in local time). Admin CRUD `/api/add-ons`. Orders are sent with the booking on create and reschedule; cancelling a booking cancels its orders. Seed: sandwiches (24 hours notice, 08:00 to 15:00) and a coffee service.
- **Tests:** two unit tests per add-on check. Integration tests for a reschedule that breaks an order's lead time (422) and cancel cascading to orders.
- **Demo:** book a room for tomorrow at 12:00 with sandwiches. Moving it to today shows "Sandwiches need 24 hours' notice".
- **Door:** one-way (migration).

### 12 Approvals
- **Branch / title:** `feat/approvals` · `feat: require approval for selected resources`
- **Why:** some resources, like the pool car, need someone to say yes.
- **Changes:** `Resource.ApproverId` (optional). Bookings on these resources start as `Pending` (which still holds the slot). `POST /api/bookings/{id}/approve` and `/reject` with a reason are compare-and-set and only allowed for the approver. `Jobs/ExpireApprovals` rejects pending bookings that reach their start time undecided. `INotifier` gets an in-app implementation (a notifications table and a bell in the UI). Produces `Pending` and `Rejected`.
- **Tests:** integration tests for a pending booking blocking the slot, approve run twice, a non-approver getting 403, and the expiry job.
- **Demo:** a member books the car and sees "Waiting for approval". The approver approves, and the member's bell shows it.
- **Door:** one-way (migration).

### 13 Check-in and auto-release
- **Branch / title:** `feat/check-in` · `feat: check in and release no-shows`
- **Why:** unused bookings hold rooms that others could use.
- **Changes:** a `CheckIn` rule kind (required yes or no, window opens N minutes before the start, release after M minutes). `POST /api/bookings/{id}/check-in` is compare-and-set. `Jobs/ReleaseNoShows` moves bookings that were not checked in to `Released`, which frees the slot. Produces `CheckedIn` and `Released`. Utilisation gains "checked-in hours" next to booked hours; the gap is the no-show waste. UI: a check-in button on "My bookings" during the window, and the extra column on Insights.
- **Tests:** unit tests for the window edges and checked-in hours. Integration tests for check-in run twice, release freeing the slot for a new booking, and checking in outside the window (409).
- **Demo:** with the clock set in the demo seed, a 09:00 no-show is released at 09:15 and the room becomes bookable again. Insights shows Room A booked for 62% but checked in for only 45%.
- **Door:** one-way (migration for the rule kind value only; settings are JSON).

### 14 Natural-language booking
- **Branch / title:** `feat/natural-language` · `feat: turn a sentence into a booking search`
- **Why:** "a room for 6 tomorrow at 2 for an hour" is faster than a form. AI only interprets the sentence; the rules decide.
- **Changes:** `IBookingIntentParser` with an OpenAI gpt-6-luna implementation (structured JSON output, today's date and the user's time zone in the prompt) and a fake for tests. `POST /api/booking-intents` returns the search fields only. UI: a single text box that fills the search form; the user checks it and searches. If there is no API key, the box is hidden.
- **Tests:** unit tests with the fake parser. Integration tests showing the endpoint never creates a booking and that a parser failure returns 422 with a "fill in the form instead" message.
- **Demo:** type "car on Friday 9 to 5" and see the form filled with pool car, Friday, 09:00 to 17:00.
- **Door:** two-way.

### 15 Handover
- **Branch / title:** `docs/handover` · `docs: add walkthrough and architecture notes`
- **Why:** reviewers need the repo, the live link and a video.
- **Changes:** README with the live link, demo accounts, how to run locally, an architecture sketch, the data model, and the API list. A link to the walkthrough video. An "Identity and calendar integration" section explains how this maps to a real Microsoft 365 setup like Resource Central's: Entra ID sign-in (OpenID Connect, users matched on the stable `oid`) replaces only the user picker, while roles, approvers and fulfillers stay in our database; an Exchange (Microsoft Graph) implementation of `ICalendarSync` maps resources to room mailboxes. Neon Auth is noted as the quickest way to switch on real sign-in.
- **Demo:** a fresh clone runs by following the README alone.
- **Door:** two-way.

## P2: only if P0 and P1 are solid

- **16 Room display** (`feat/room-display`): a full-screen page per resource showing now, next and a check-in button.
- **17 Visitor pre-registration** (`feat/visitors`): add visitor names to a booking, with a reception list.
- **18 Recurring bookings** (`feat/recurring`): a weekly pattern creates a series of bookings. Each one is judged separately, and clashes are reported with alternatives.
- **19 Mocked calendar sync** (`feat/calendar-sync`): `ICalendarSync` with a fake that logs create, update and cancel.
- **20 AI insight summary** (`feat/insight-summary`): a paragraph summarising utilisation, written from the numbers that 08 computes.

## Future updates

Planned and specified, but deliberately left until after the handover. Each can be picked up as a thin PR on its own.

### 11 Fulfiller queue
- **Why it moved:** add-ons (10) already deliver the user-facing value: ordering with a booking, with lead time and delivery hours enforced, and cancellation cascading. The queue serves the back office (the catering team) rather than the people booking. Approvals (12) and check-in (13) mattered more for the brief's focus on constraints and utilisation. `AddOn.FulfillerId` and the `Delivered` order status are already in place, so nothing needs rework.
- **Branch / title:** `feat/fulfiller-queue` · `feat: add fulfiller queue for add-on orders`
- **Why:** someone has to deliver the catering.
- **Changes:** `GET /api/add-on-orders?mine-to-fulfil=true` (paginated, ordered by delivery time) and `POST /api/add-on-orders/{id}/deliver` (compare-and-set `Requested` to `Delivered`). Orders on cancelled or rejected bookings are already `Cancelled`, so they drop out. UI: a Fulfiller page grouped by day.
- **Tests:** integration tests for a fulfiller seeing only their orders and deliver run twice.
- **Demo:** sign in as the catering fulfiller, see tomorrow's sandwiches, mark them delivered.
- **Door:** two-way.

## Coverage check

| Must hold | Owning PR |
| --- | --- |
| Store UTC; judge in the location's time zone | 03 (zone), 04 (UTC), 07 (local-time rules) |
| Half-open intervals | 04 |
| Exclusion constraint; `23P01` becomes 409 | 04 |
| Concurrency: exactly one wins | 04 |
| `Idempotency-Key` on create | 04 |
| `Version` token; compare-and-set status changes | 05 (then 08, 12, 13; 11 in Future updates) |
| Reschedule excludes itself and re-runs every rule | 05, 06 |
| Rules judged for the owner | 06, 07 |
| Two unit tests per rule | 06, 07, 10, 13 |
| ProblemDetails with `code`; 401 and 403 | 02 |
| 422 with `violations` | 06 |
| `alternatives` | 09 |
| Paginated lists with a stable order | 02 (pattern), then every list |
| `IClock`, never `DateTime.UtcNow` | 04 |

| Status | Produced in |
| --- | --- |
| Confirmed | 04 |
| Cancelled | 05 |
| Completed | 08 |
| Pending, Rejected | 12 |
| CheckedIn, Released | 13 |

## Decisions

1. **Sign-in is a user picker, built properly.** Picking a user issues a standard ASP.NET cookie with role claims, so authorisation is ordinary `[Authorize(Roles = "Admin")]` and real SSO would replace only the sign-in step. There are no passwords; an admin "Reset demo data" action restores the seed if a visitor messes it up.
2. **Roles are `Admin` and `Member`.** Approvers are assigned per resource (`ApproverId`) and fulfillers per add-on (`FulfillerId`).
3. **Utilisation is booked hours divided by open hours.** Booked hours count `Confirmed`, `CheckedIn` and `Completed`, clipped to the range. Open hours come from the opening-hours rule (24 hours if there is none) and are calculated per local day, so daylight saving doesn't skew them. This is why 07 blocks 08. Checked-in hours are added in 13.
4. **Hosting is Render**, with Postgres on Neon.
