import { FormEvent, useEffect, useState } from "react";
import { api, describeError, Alternative, ApiError, Booking, Resource, ResourceBooking } from "../../api";
import { dayRange, formatTime, localToIso, todayIn } from "../../time";

// One resource: the day's bookings and a form to book it. Times are in the location's time zone.
export function ResourcePage({ resource, onBack }: { resource: Resource; onBack: () => void }) {
  const [date, setDate] = useState(todayIn(resource.timeZone));
  const [bookings, setBookings] = useState<ResourceBooking[]>([]);
  const [startTime, setStartTime] = useState("09:00");
  const [endTime, setEndTime] = useState("10:00");
  const [attendees, setAttendees] = useState("");
  const [errors, setErrors] = useState<string[]>([]);
  const [message, setMessage] = useState("");
  const [alternatives, setAlternatives] = useState<Alternative[]>([]);

  useEffect(() => {
    loadDay();
  }, [date]);

  async function loadDay() {
    const { from, to } = dayRange(date, resource.timeZone);
    try {
      setBookings(await api<ResourceBooking[]>("GET", `/api/resources/${resource.id}/bookings?from=${from}&to=${to}`));
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  function book(event: FormEvent) {
    event.preventDefault();
    send(resource.id, localToIso(date, startTime, resource.timeZone), localToIso(date, endTime, resource.timeZone));
  }

  // Used by the form and by the alternative buttons (which may be another resource).
  async function send(resourceId: string, start: string, end: string) {
    setErrors([]);
    setMessage("");
    setAlternatives([]);
    try {
      const created = await api<Booking>("POST", "/api/bookings", {
        resourceId,
        start,
        end,
        attendees: attendees === "" ? null : Number(attendees),
      });
      setMessage(`Booked ${created.resourceName} from ${formatTime(created.start, resource.timeZone)} to ${formatTime(created.end, resource.timeZone)}.`);
    } catch (error) {
      setErrors(describeError(error));
      if (error instanceof ApiError) setAlternatives(error.alternatives);
    }
    await loadDay();
  }

  return (
    <section>
      <p><button onClick={onBack}>Back to resources</button></p>
      <h2>{resource.name}</h2>
      <p>{resource.resourceTypeName} at {resource.locationName} (times in {resource.timeZone})</p>

      <p>
        <label>Day{" "}
          <input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
        </label>
      </p>
      <h3>Bookings that day</h3>
      {bookings.length === 0 ? <p>Nothing is booked. The whole day is free.</p> : (
        <ul>
          {bookings.map((b) => (
            <li key={b.id}>
              {formatTime(b.start, resource.timeZone)} to {formatTime(b.end, resource.timeZone)}, {b.ownerName} ({b.status})
            </li>
          ))}
        </ul>
      )}

      <h3>Book it</h3>
      <form onSubmit={book}>
        <p>
          <label>Start{" "}
            <input type="time" value={startTime} onChange={(e) => setStartTime(e.target.value)} required />
          </label>{" "}
          <label>End{" "}
            <input type="time" value={endTime} onChange={(e) => setEndTime(e.target.value)} required />
          </label>{" "}
          <label>Attendees{" "}
            <input type="number" min={1} value={attendees} onChange={(e) => setAttendees(e.target.value)} style={{ width: "5rem" }} />
          </label>{" "}
          <button type="submit">Book</button>
        </p>
      </form>
      {errors.map((message) => (
        <p key={message} style={{ color: "#b00020" }}>{message}</p>
      ))}
      {alternatives.length > 0 && (
        <div>
          <p>These are free and allowed instead:</p>
          <p>
            {alternatives.map((a) => (
              <button key={a.resourceId + a.start} onClick={() => send(a.resourceId, a.start, a.end)} style={{ marginRight: 8 }}>
                Book {a.resourceName}, {formatTime(a.start, resource.timeZone)} to {formatTime(a.end, resource.timeZone)}
              </button>
            ))}
          </p>
        </div>
      )}
      {message && <p>{message}</p>}
    </section>
  );
}
