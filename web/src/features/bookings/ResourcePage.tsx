import { FormEvent, useEffect, useState } from "react";
import { api, describeError, AddOn, Alternative, ApiError, Booking, Paged, Resource, ResourceBooking } from "../../api";
import { dayName, dayRange, formatDate, formatTime, fromMinutes, localToIso, todayIn, toMinutes, weekdayFrom } from "../../time";
import { Messages } from "../../ui";
import { DayTimeline, OpenHours } from "./DayTimeline";

type Rule = { kind: string; settings: { opens?: string; closes?: string; days?: string[] } };

// 09:00, or later today: the next whole hour on the zone's clock.
function defaultStart(date: string, timeZone: string): string {
  if (date !== todayIn(timeZone)) return "09:00";
  const now = toMinutes(formatTime(new Date().toISOString(), timeZone));
  return fromMinutes(Math.min(23 * 60, Math.max(9 * 60, Math.ceil((now + 1) / 60) * 60)));
}

// A time someone searched for, carried into the booking form. Local date and HH:mm times.
export type Wanted = { date: string; start: string; end: string; attendees: string };

type Props = { resource: Resource; wanted?: Wanted; onBack: () => void };

// One resource: its day as a timeline and a form to book it. Times are in the location's time zone.
export function ResourcePage({ resource, wanted, onBack }: Props) {
  const firstDay = wanted?.date ?? weekdayFrom(todayIn(resource.timeZone));
  const firstStart = wanted?.start ?? defaultStart(firstDay, resource.timeZone);
  const [date, setDate] = useState(firstDay);
  const [bookings, setBookings] = useState<ResourceBooking[]>([]);
  const [rules, setRules] = useState<Rule[]>([]);
  const [startTime, setStartTime] = useState(firstStart);
  const [endTime, setEndTime] = useState(wanted?.end ?? fromMinutes(toMinutes(firstStart) + 60));
  const [attendees, setAttendees] = useState(wanted?.attendees ?? "");
  const [errors, setErrors] = useState<string[]>([]);
  const [message, setMessage] = useState("");
  const [alternatives, setAlternatives] = useState<Alternative[]>([]);
  const [addOns, setAddOns] = useState<AddOn[]>([]);
  // Quantity typed per add-on id; empty or 0 means not ordered.
  const [quantities, setQuantities] = useState<Record<string, string>>({});

  useEffect(() => {
    api<Rule[]>("GET", `/api/resources/${resource.id}/rules`).then(setRules).catch(() => setRules([]));
    api<Paged<AddOn>>("GET", `/api/add-ons?locationId=${resource.locationId}&pageSize=100`)
      .then((page) => setAddOns(page.items)).catch(() => setAddOns([]));
  }, [resource.id]);

  const ordered = addOns
    .filter((a) => Number(quantities[a.id] || 0) > 0)
    .map((a) => ({ addOnId: a.id, quantity: Number(quantities[a.id]) }));

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

  // Opening hours for the chosen day, from the resource's effective rule.
  const hoursRule = rules.find((r) => r.kind === "OpeningHours")?.settings;
  const openHours: OpenHours = !hoursRule ? null
    : hoursRule.days && !hoursRule.days.includes(dayName(date)) ? "closed"
    : { opens: hoursRule.opens ?? "00:00", closes: hoursRule.closes ?? "23:59" };

  // A click on the timeline keeps the booking's length and moves its start.
  function pickStart(start: string) {
    const length = Math.max(15, toMinutes(endTime) - toMinutes(startTime));
    setStartTime(start);
    setEndTime(fromMinutes(toMinutes(start) + length));
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
        // Add-ons belong to this resource's location, so they only go with a booking here.
        addOns: resourceId === resource.id ? ordered : [],
      });
      const extras = created.addOns.length > 0 ? ` With ${created.addOns.map((o) => `${o.quantity} × ${o.name}`).join(", ")}.` : "";
      const when = `${formatDate(created.start, resource.timeZone)}, ${formatTime(created.start, resource.timeZone)} to ${formatTime(created.end, resource.timeZone)}`;
      setMessage(created.status === "Pending"
        ? `Requested ${created.resourceName} on ${when}.${extras} It needs approval: it holds the slot meanwhile, and you'll get a notification.`
        : `Booked ${created.resourceName} on ${when}.${extras} It's in My bookings.`);
      setQuantities({});
    } catch (error) {
      setErrors(describeError(error));
      if (error instanceof ApiError) setAlternatives(error.alternatives);
    }
    await loadDay();
  }

  return (
    <section>
      <p><button className="link" onClick={onBack}>← All resources</button></p>
      <h2>{resource.name}</h2>
      <p className="muted">
        {resource.resourceTypeName} · {resource.locationName} · {resource.capacity ? `up to ${resource.capacity} people` : "no people limit"} · times in {resource.timeZone}
        {hoursRule && ` · open ${hoursRule.opens} to ${hoursRule.closes}, ${hoursRule.days?.join(" ")}`}
        {resource.approverId && " · bookings need approval"}
      </p>

      <div className="card">
        <form onSubmit={book} className="row">
          <label>Day
            <input type="date" value={date} onChange={(e) => setDate(e.target.value)} required />
          </label>
          <label>Start
            <input type="time" step={900} value={startTime} onChange={(e) => setStartTime(e.target.value)} required />
          </label>
          <label>End
            <input type="time" step={900} value={endTime} onChange={(e) => setEndTime(e.target.value)} required />
          </label>
          <label>Attendees
            <input type="number" min={1} value={attendees} placeholder="optional" onChange={(e) => setAttendees(e.target.value)} style={{ width: "6rem" }} />
          </label>
          <button type="submit">Book {resource.name}</button>
        </form>
        {addOns.length > 0 && (
          <div className="row" style={{ marginTop: "0.75rem" }}>
            <span className="muted" style={{ alignSelf: "center" }}>Order with it:</span>
            {addOns.map((a) => (
              <label key={a.id} title={`Order ${Math.round(a.leadTimeMinutes / 60)} hours ahead; for bookings starting ${a.availableFrom} to ${a.availableUntil}`}>
                {a.name} <span>(needs {a.leadTimeMinutes >= 60 ? `${Math.round(a.leadTimeMinutes / 60)} h` : `${a.leadTimeMinutes} min`} notice, {a.availableFrom}–{a.availableUntil})</span>
                <input type="number" min={0} placeholder="0" value={quantities[a.id] ?? ""} style={{ width: "6rem" }}
                  onChange={(e) => setQuantities({ ...quantities, [a.id]: e.target.value })} />
              </label>
            ))}
          </div>
        )}

        <DayTimeline date={date} timeZone={resource.timeZone} bookings={bookings} openHours={openHours}
          selectionStart={startTime} selectionEnd={endTime} onPick={pickStart} />
        <p className="muted" style={{ margin: 0, fontSize: "0.85rem" }}>
          {openHours === "closed" ? `${resource.name} is closed on this day. ` : ""}
          {bookings.length === 0 ? "Nothing is booked yet. " : `${bookings.length} booking${bookings.length === 1 ? "" : "s"} this day. `}
          Click a free spot on the bar to move your booking there.
        </p>

        <Messages errors={errors} success={message} />
        {alternatives.length > 0 && (
          <div>
            <p style={{ marginBottom: "0.35rem" }}>These are free and allowed instead:</p>
            <div className="row">
              {alternatives.map((a) => (
                <button key={a.resourceId + a.start} onClick={() => send(a.resourceId, a.start, a.end)}>
                  Book {a.resourceName}, {formatTime(a.start, resource.timeZone)} to {formatTime(a.end, resource.timeZone)}
                </button>
              ))}
            </div>
          </div>
        )}
      </div>

      {bookings.length > 0 && (
        <table>
          <thead><tr><th>Time</th><th>Booked by</th></tr></thead>
          <tbody>
            {bookings.map((b) => (
              <tr key={b.id}>
                <td>{formatTime(b.start, resource.timeZone)} to {formatTime(b.end, resource.timeZone)}</td>
                <td>{b.ownerName}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
