# Glossary

The code, UI and docs use these terms. "Meeting room" or "catering" are data an admin enters, never words in the code.

| Term | Meaning |
| --- | --- |
| Location | A place (for example a building) with an IANA time zone. Opening hours, menus and quotas are judged in its local time. |
| Resource type | A category of resource with default rules, for example "Meeting room" or "Car". |
| Resource | One bookable thing at a location, for example "Room 4.01". It can override its type's rules and may have an approver. |
| Rule | A constraint an admin configures on a resource type or resource, for example a maximum duration or opening hours. |
| Booking | A user's claim on one resource for a time range `[start, end)`. |
| Add-on | A service ordered for a booking, for example catering. It has notice and hours rules and a fulfiller. It is not a resource. |

## Booking statuses

| Status | Holds the slot? | Meaning |
| --- | --- | --- |
| Pending | Yes | Waiting for the resource's approver. |
| Confirmed | Yes | Approved, or no approval needed. |
| CheckedIn | Yes | The user has arrived. |
| Completed | No | The booking has finished. |
| Cancelled | No | The user or an admin cancelled it. |
| Rejected | No | The approver declined it. |
| Released | No | Not checked in in time, so the slot was freed. |
