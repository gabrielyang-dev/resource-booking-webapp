import { FormEvent, useEffect, useState } from "react";
import { api, describeError, Booking, Paged } from "../../api";
import { formatDate, formatTime, localToIso } from "../../time";

const pageSize = 50;
const holdingStatuses = ["Pending", "Confirmed", "CheckedIn"];

// The local date and time of a UTC instant in the zone: "2030-06-10", "10:00".
function localParts(iso: string, timeZone: string): { date: string; time: string } {
  return {
    date: new Date(iso).toLocaleDateString("en-CA", { timeZone }),
    time: formatTime(iso, timeZone),
  };
}

// The signed-in user's bookings, upcoming first then past, in each location's local time.
export function MyBookingsPage() {
  const [bookings, setBookings] = useState<Booking[]>([]);
  const [errors, setErrors] = useState<string[]>([]);
  const [editing, setEditing] = useState<Booking | null>(null);
  const [date, setDate] = useState("");
  const [startTime, setStartTime] = useState("");
  const [endTime, setEndTime] = useState("");
  const [attendees, setAttendees] = useState("");

  useEffect(() => {
    load();
  }, []);

  async function load() {
    try {
      const result = await api<Paged<Booking>>("GET", `/api/bookings?mine=true&pageSize=${pageSize}`);
      setBookings(result.items);
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
      await load();
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  async function cancel(b: Booking) {
    setErrors([]);
    try {
      await api<Booking>("POST", `/api/bookings/${b.id}/cancel`);
      await load();
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  const now = Date.now();
  const upcoming = bookings.filter((b) => Date.parse(b.end) > now);
  const past = bookings.filter((b) => Date.parse(b.end) <= now).reverse();

  function table(rows: Booking[]) {
    return (
      <table cellPadding={6}>
        <thead>
          <tr><th align="left">Resource</th><th align="left">Date</th><th align="left">Time</th><th align="left">Status</th><th></th></tr>
        </thead>
        <tbody>
          {rows.map((b) => (
            <tr key={b.id}>
              <td>{b.resourceName} ({b.locationName})</td>
              <td>{formatDate(b.start, b.timeZone)}</td>
              <td>{formatTime(b.start, b.timeZone)} to {formatTime(b.end, b.timeZone)}</td>
              <td>{b.status}</td>
              <td>
                {holdingStatuses.includes(b.status) && Date.parse(b.end) > now && (
                  <>
                    <button onClick={() => startEditing(b)}>Edit</button>{" "}
                    <button onClick={() => cancel(b)}>Cancel booking</button>
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
        <form onSubmit={save}>
          <h3>Change {editing.resourceName} (times in {editing.timeZone})</h3>
          <p>
            <label>Day <input type="date" value={date} onChange={(e) => setDate(e.target.value)} required /></label>{" "}
            <label>Start <input type="time" value={startTime} onChange={(e) => setStartTime(e.target.value)} required /></label>{" "}
            <label>End <input type="time" value={endTime} onChange={(e) => setEndTime(e.target.value)} required /></label>{" "}
            <label>Attendees <input type="number" min={1} value={attendees} onChange={(e) => setAttendees(e.target.value)} style={{ width: "5rem" }} /></label>{" "}
            <button type="submit">Save</button>{" "}
            <button type="button" onClick={() => setEditing(null)}>Close</button>
          </p>
        </form>
      )}
      {errors.map((message) => (
        <p key={message} style={{ color: "#b00020" }}>{message}</p>
      ))}
      <h3>Upcoming</h3>
      {upcoming.length === 0 ? <p>You have no upcoming bookings.</p> : table(upcoming)}
      <h3>Past</h3>
      {past.length === 0 ? <p>You have no past bookings.</p> : table(past)}
    </section>
  );
}
