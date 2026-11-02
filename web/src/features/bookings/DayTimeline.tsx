import { MouseEvent } from "react";
import { ResourceBooking } from "../../api";
import { formatTime, fromMinutes, localDate, toMinutes } from "../../time";

export type OpenHours = { opens: string; closes: string } | "closed" | null;

type Props = {
  date: string; // yyyy-mm-dd, local to the resource
  timeZone: string;
  bookings: ResourceBooking[];
  openHours: OpenHours; // null = no opening-hours rule (open all day)
  selectionStart: string; // "HH:mm" from the form
  selectionEnd: string;
  onPick: (start: string) => void; // a click on the bar, rounded down to 15 minutes
};

const dayMinutes = 24 * 60;

// One local day as a horizontal bar: booked blocks, closed hours shaded, and the form's times
// as a dashed box (red when it clashes). Clicking a free spot moves the form's start time there.
export function DayTimeline({ date, timeZone, bookings, openHours, selectionStart, selectionEnd, onPick }: Props) {
  // Show a little either side of opening hours; without a rule, 06:00 to 22:00.
  const opens = openHours && openHours !== "closed" ? toMinutes(openHours.opens) : null;
  const closes = openHours && openHours !== "closed" ? toMinutes(openHours.closes) : null;
  const viewStart = opens === null ? 6 * 60 : Math.max(0, Math.floor(opens / 60) * 60 - 60);
  const viewEnd = closes === null ? 22 * 60 : Math.min(dayMinutes, Math.ceil(closes / 60) * 60 + 60);
  const span = viewEnd - viewStart;

  // Percent from the left for a number of minutes after local midnight.
  const left = (minutes: number) => `${((Math.max(viewStart, Math.min(viewEnd, minutes)) - viewStart) / span) * 100}%`;
  const width = (from: number, to: number) =>
    `${((Math.min(viewEnd, to) - Math.max(viewStart, from)) / span) * 100}%`;

  // A booking that starts the day before or ends the day after is cut at midnight.
  function minutesOf(iso: string, edge: number): number {
    return localDate(iso, timeZone) === date ? toMinutes(formatTime(iso, timeZone)) : edge;
  }

  const blocks = bookings.map((b) => ({
    booking: b,
    from: minutesOf(b.start, 0),
    to: minutesOf(b.end, dayMinutes),
  }));
  const selFrom = toMinutes(selectionStart);
  const selTo = toMinutes(selectionEnd);
  const clashes = blocks.some((b) => b.from < selTo && selFrom < b.to);

  function pick(event: MouseEvent<HTMLDivElement>) {
    const rect = event.currentTarget.getBoundingClientRect();
    const minutes = viewStart + ((event.clientX - rect.left) / rect.width) * span;
    onPick(fromMinutes(Math.floor(minutes / 15) * 15));
  }

  const hours: number[] = [];
  for (let h = Math.ceil(viewStart / 60); h <= Math.floor(viewEnd / 60); h++) hours.push(h);

  return (
    <div className="timeline" onClick={pick} title="Click a free time to start your booking there">
      {openHours === "closed" && <div className="closed" style={{ left: 0, width: "100%" }} />}
      {opens !== null && <div className="closed" style={{ left: 0, width: width(viewStart, opens) }} />}
      {closes !== null && <div className="closed" style={{ left: left(closes), width: width(closes, viewEnd) }} />}
      {hours.map((h) => (
        <span key={h}>
          <span className="tick" style={{ left: left(h * 60) }} />
          <span className="hour" style={{ left: left(h * 60) }}>{String(h).padStart(2, "0")}</span>
        </span>
      ))}
      {blocks.filter((b) => b.to > viewStart && b.from < viewEnd).map(({ booking, from, to }) => (
        <div key={booking.id} className="block" style={{ left: left(from), width: width(from, to) }}
          title={`${formatTime(booking.start, timeZone)} to ${formatTime(booking.end, timeZone)}, ${booking.ownerName}`}
          onClick={(e) => e.stopPropagation()}>
          {booking.ownerName}
        </div>
      ))}
      {selTo > selFrom && (
        <div className={`selection${clashes ? " clash" : ""}`} style={{ left: left(selFrom), width: width(selFrom, selTo) }} />
      )}
    </div>
  );
}
