# Glossary

The code, UI and docs use these terms. The code speaks only in generic terms: "meeting room", "pool car" or "catering" are data an admin enters, never words in the code.

## Catalogue

| Term | Meaning |
| --- | --- |
| Location | A place, such as a building, with an IANA time zone. Opening hours, add-on hours and daily quotas are judged in its local time. |
| Resource type | A kind of resource, such as "Meeting room" or "Car". It holds the default rules for its resources. |
| Resource | One bookable thing at a location, such as "Room A". It has an optional capacity, can override its type's rules and may have an approver. Resources are deactivated, never deleted. |
| Add-on | A service ordered *for* a booking, such as sandwiches. It is not a resource: it is never held for a time range. It needs notice (a lead time), is served only at certain local hours, and may name a fulfiller. |
| Add-on order | One add-on, with a quantity, ordered for one booking. Status `Requested`, `Delivered` or `Cancelled`. Cancelling a booking cancels its orders. |

## People

| Term | Meaning |
| --- | --- |
| Admin | A role. Manages users, the catalogue and rules, sees insights, and can act on any booking. |
| Member | A role. Books resources and manages their own bookings. |
| Owner | The person a booking is for. Rules are always judged for the owner, even when an admin makes the change. |
| Approver | A user named on a resource whose bookings need a yes. Assigned per resource, not a role. |
| Fulfiller | A user named on an add-on who delivers it. Assigned per add-on, not a role. Their order queue is a future update. |

## Bookings

| Term | Meaning |
| --- | --- |
| Booking | A claim on one resource for a time range, with an owner, a status and optional attendees and add-on orders. |
| Time range | A half-open interval `[start, end)`, stored in UTC. A booking ending at 10:00 and one starting at 10:00 do not overlap. |
| Holding status | A status that keeps the slot: `Pending`, `Confirmed` or `CheckedIn`. Postgres refuses two holding bookings that overlap on the same resource. |
| Reschedule | Changing a booking's resource, times, attendees or add-ons. The booking is checked again against every rule, ignoring its own slot. |
| Check-in | The owner saying they've arrived, allowed from a set time before the start. |
| Release | Freeing a booking nobody checked in to, a set time after its start, so someone else can book the slot. |
| Version | A number on each booking that changes with every edit. An edit carrying an old version is refused (`STALE_VERSION`), so two people can't overwrite each other. |
| Idempotency key | A key sent with a new booking. Sending the same request again returns the first booking instead of making a second. |
| Booking intent | Search fields (resource type, day, times, attendees) read from one sentence by the AI. The user checks them before searching; the AI never books. |

### Statuses

| Status | Holds the slot? | How it gets there |
| --- | --- | --- |
| Pending | Yes | Created on a resource with an approver; waiting for a decision. |
| Confirmed | Yes | Created on a resource without an approver, or approved. |
| CheckedIn | Yes | The owner checked in. |
| Completed | No | Its end time passed while Confirmed or CheckedIn. |
| Cancelled | No | The owner or an admin cancelled it. |
| Rejected | No | The approver said no, or nobody decided before it started. A reason is kept. |
| Released | No | Its resource requires check-in and nobody checked in in time. |

## Rules

| Term | Meaning |
| --- | --- |
| Check | A constraint that always applies: a valid time range, capacity, and an add-on's lead time and hours. |
| Rule | A constraint an admin configures, of one kind, with settings stored as JSON. |
| Type default | A rule set on a resource type. It applies to every resource of that type. |
| Override | A rule set on one resource. It replaces the type default of the same kind. |
| Effective rule | What actually applies to a resource: its override if it has one, otherwise its type default. |
| Violation | One reason a booking isn't allowed, in plain language that says how to fix it. Every violation is returned at once. |
| Alternative | A free time that passes every rule, offered with a rejection: a nearby time on the same resource, or another resource of the same type. |

### Rule kinds

| Kind | Settings | Meaning |
| --- | --- | --- |
| OpeningHours | opens, closes, days | Bookings must fit inside these local hours on these days. |
| MaxDuration | max minutes | The longest a single booking may be. |
| BookingWindow | minimum notice, maximum days ahead | How soon and how far ahead a booking may start. |
| DailyQuota | max minutes per day | How long one person may book this resource in one local day. |
| CheckIn | required, opens minutes before, release after minutes | Whether bookings need check-in, when it opens, and when no-shows are released. |

## Insights

| Term | Meaning |
| --- | --- |
| Open hours | The hours a resource could be used in a date range: from its effective opening hours, or 24 hours a day without them. Counted per local day, so clock changes don't skew them. |
| Booked hours | Hours of `Confirmed`, `CheckedIn` and `Completed` bookings, clipped to the date range. |
| Utilisation | Booked hours divided by open hours. |
| Checked-in hours | Booked hours where someone checked in. |
| No-show hours | Hours of released bookings: space that was held but not used. |
