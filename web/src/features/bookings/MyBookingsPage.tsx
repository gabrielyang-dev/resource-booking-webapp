import { FormEvent, useEffect, useState } from "react";
import { api, describeError, Booking, Paged } from "../../api";
import { formatDate, formatTime, localToIso } from "../../time";
import { Messages, StatusBadge } from "../../ui";

const pageSize = 50;
const holdingStatuses = ["Pending", "Confirmed", "CheckedIn"];

// The local date and time of a UTC instant in the zone: "2030-06-10", "10:00".
function localParts(iso: string, timeZone: string): { date: string; time: string } {
  return {
    date: new Date(iso).toLocaleDateString("en-CA", { timeZone }),
    time: formatTime(iso, timeZone),
  };
}

// The signed-in user's bookings: upcoming, then past, in each location's local time.
export function MyBookingsPage() {
  const [upcoming, setUpcoming] = useState<Booking[]>([]);
  const [past, setPast] = useState<Paged<Booking> | null>(null);
  const [pastPageSize, setPastPageSize] = useState(10);
  const [errors, setErrors] = useState<string[]>([]);
  const [message, setMessage] = useState("");
  const [editing, setEditing] = useState<Booking | null>(null);
  const [date, setDate] = useState("");
  const [startTime, setStartTime] = useState("");
  const [endTime, setEndTime] = useState("");
  const [attendees, setAttendees] = useState("");

  useEffect(() => {
    load();
  }, [pastPageSize]);

  // Two lists from the server: what's coming (soonest first) and what's done (most recent first).
  async function load() {
    try {
      setUpcoming((await api<Paged<Booking>>("GET", `/api/bookings?mine=true&when=upcoming&pageSize=${pageSize}`)).items);
      setPast(await api<Paged<Booking>>("GET", `/api/bookings?mine=true&when=past&pageSize=${pastPageSize}`));
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  function startEditing(b: Booking) {
    setErrors([]);
    setEditing(b);
    setDate(localParts(b.start, b.timeZone).date);
    setStartTime(localParts(b.start, b.timeZone).time);
    setEndTime(localParts(b.end, b.timeZone).time);
    setAttendees(b.attendees === null ? "" : String(b.attendees));
  }

  async function save(event: FormEvent) {
    event.preventDefault();
    if (!editing) return;
    setErrors([]);
    try {
      // The version we opened goes back to the server; if someone changed the booking since, it says so.
      await api<Booking>("PUT", `/api/bookings/${editing.id}`, {
        resourceId: editing.resourceId,
        start: localToIso(date, startTime, editing.timeZone),
        end: localToIso(date, endTime, editing.timeZone),
        attendees: attendees === "" ? null : Number(attendees),
        version: editing.version,
      });
      setEditing(null);
      setMessage(`Saved: ${editing.resourceName}, ${date} ${startTime} to ${endTime}.`);
      await load();
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  async function cancel(b: Booking) {
    const when = `${formatDate(b.start, b.timeZone)}, ${formatTime(b.start, b.timeZone)} to ${formatTime(b.end, b.timeZone)}`;
    if (!window.confirm(`Cancel ${b.resourceName} on ${when}? The slot is freed for others.`)) {
      return;
    }
    setErrors([]);
    setMessage("");
    try {
      await api<Booking>("POST", `/api/bookings/${b.id}/cancel`);
      setMessage(`Cancelled ${b.resourceName} on ${when}.`);
      await load();
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  // The server decides whether check-in is open; it says when it opens if you are early.
  async function checkIn(b: Booking) {
    setErrors([]);
    setMessage("");
    try {
      await api<Booking>("POST", `/api/bookings/${b.id}/check-in`);
      setMessage(`Checked in to ${b.resourceName}. Enjoy your meeting.`);
      await load();
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  const now = Date.now();
  // Show the button from 15 minutes before the start until the end (the usual check-in window).
  const canCheckIn = (b: Booking) =>
    b.status === "Confirmed" && now >= Date.parse(b.start) - 15 * 60 * 1000 && now < Date.parse(b.end);

  function table(rows: Booking[]) {
    return (
      <table>
        <thead>
          <tr><th>Resource</th><th>Date</th><th>Time</th><th>Add-ons</th><th>Status</th><th></th></tr>
        </thead>
        <tbody>
          {rows.map((b) => (
            <tr key={b.id}>
              <td>{b.resourceName} ({b.locationName})</td>
              <td>{formatDate(b.start, b.timeZone)}</td>
              <td>{formatTime(b.start, b.timeZone)} to {formatTime(b.end, b.timeZone)}</td>
              <td>{b.addOns.filter((o) => o.status !== "Cancelled").map((o) => `${o.quantity} × ${o.name}`).join(", ") || <span className="muted">none</span>}</td>
              <td><StatusBadge status={b.status} /></td>
              <td>
                {holdingStatuses.includes(b.status) && Date.parse(b.end) > now && (
                  <>
                    {canCheckIn(b) && <><button className="primary" onClick={() => checkIn(b)}>Check in</button>{" "}</>}
                    <button onClick={() => startEditing(b)}>Edit</button>{" "}
                    <button className="danger" onClick={() => cancel(b)}>Cancel booking</button>
                  </>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    );
  }

  return (
    <section>
      <h2>My bookings</h2>
      {editing && (
        <form onSubmit={save} className="card">
          <h3 style={{ marginTop: 0 }}>Change {editing.resourceName} <span className="muted">(times in {editing.timeZone})</span></h3>
          <div className="row">
            <label>Day <input type="date" value={date} onChange={(e) => setDate(e.target.value)} required /></label>
            <label>Start <input type="time" step={900} value={startTime} onChange={(e) => setStartTime(e.target.value)} required /></label>
            <label>End <input type="time" step={900} value={endTime} onChange={(e) => setEndTime(e.target.value)} required /></label>
            <label>Attendees <input type="number" min={1} value={attendees} onChange={(e) => setAttendees(e.target.value)} style={{ width: "6rem" }} /></label>
            <button type="submit">Save changes</button>
            <button type="button" onClick={() => setEditing(null)}>Close</button>
          </div>
        </form>
      )}
      <Messages errors={errors} success={message} />
      <h3>Upcoming</h3>
      {upcoming.length === 0 ? <p className="empty">You have no upcoming bookings. Use Find a resource to book one.</p> : table(upcoming)}
      <h3>Past</h3>
      {!past || past.items.length === 0 ? <p className="empty">You have no past bookings.</p> : table(past.items)}
      {past && past.totalCount > past.items.length && (
        <button onClick={() => setPastPageSize(Math.min(100, pastPageSize + 20))} disabled={pastPageSize >= 100}>
          Show more ({past.items.length} of {past.totalCount})
        </button>
      )}
    </section>
  );
}
