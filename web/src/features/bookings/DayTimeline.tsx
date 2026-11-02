import { CSSProperties, MouseEvent } from "react";
import { ResourceBooking } from "../../api";
import { formatTime, fromMinutes, localDate, todayIn, toMinutes } from "../../time";
import { typeColour } from "../../ui";

export type OpenHours = { opens: string; closes: string } | "closed" | null;

type Props = {
  date: string; // yyyy-mm-dd, local to the resource
  timeZone: string;
  bookings: ResourceBooking[];
  openHours: OpenHours; // null = no opening-hours rule (open all day)
  typeName: string; // colours the blocks, like signage for that kind of space
  onPick: (start: string) => void; // a click on the bar, rounded down to 15 minutes
  selectionStart?: string; // "HH:mm" from the form; left out on the office board
  selectionEnd?: string;
  compact?: boolean; // a short row on the office board: no hour numbers
  viewStart?: number; // minutes after midnight; the board passes one span for every row
  viewEnd?: number;
};

const dayMinutes = 24 * 60;

// The span to show: a little either side of opening hours, or 07:00 to 20:00 without a rule.
export function viewFor(openHours: OpenHours): { start: number; end: number } {
  if (!openHours || openHours === "closed") return { start: 7 * 60, end: 20 * 60 };
  return {
    start: Math.max(0, Math.floor(toMinutes(openHours.opens) / 60) * 60 - 60),
    end: Math.min(dayMinutes, Math.ceil(toMinutes(openHours.closes) / 60) * 60 + 60),
  };
}

// One local day as a bar: bookings as blocks, closed hours hatched, "now" as a yellow line,
// and the form's times as a yellow plate (hatched red when it lands on someone else's booking).
// Clicking a free spot moves the start time there.
export function DayTimeline(props: Props) {
  const { date, timeZone, bookings, openHours, typeName, onPick, selectionStart, selectionEnd, compact } = props;
  const fallback = viewFor(openHours);
  const viewStart = props.viewStart ?? fallback.start;
  const viewEnd = props.viewEnd ?? fallback.end;
  const span = viewEnd - viewStart;

  const opens = openHours && openHours !== "closed" ? toMinutes(openHours.opens) : null;
  const closes = openHours && openHours !== "closed" ? toMinutes(openHours.closes) : null;

  // Percent from the left for a number of minutes after local midnight.
  const left = (minutes: number) => `${((Math.max(viewStart, Math.min(viewEnd, minutes)) - viewStart) / span) * 100}%`;
  const width = (from: number, to: number) =>
    `${(Math.max(0, Math.min(viewEnd, to) - Math.max(viewStart, from)) / span) * 100}%`;

  // A booking that starts the day before or ends the day after is cut at midnight.
  function minutesOf(iso: string, edge: number): number {
    return localDate(iso, timeZone) === date ? toMinutes(formatTime(iso, timeZone)) : edge;
  }

  const blocks = bookings.map((b) => ({ booking: b, from: minutesOf(b.start, 0), to: minutesOf(b.end, dayMinutes) }));
  const hasSelection = selectionStart !== undefined && selectionEnd !== undefined;
  const selFrom = hasSelection ? toMinutes(selectionStart) : 0;
  const selTo = hasSelection ? toMinutes(selectionEnd) : 0;
  const clashes = blocks.some((b) => b.from < selTo && selFrom < b.to);

  const isToday = date === todayIn(timeZone);
  const now = isToday ? toMinutes(formatTime(new Date().toISOString(), timeZone)) : null;

  function pick(event: MouseEvent<HTMLDivElement>) {
    const rect = event.currentTarget.getBoundingClientRect();
    const minutes = viewStart + ((event.clientX - rect.left) / rect.width) * span;
    onPick(fromMinutes(Math.floor(minutes / 15) * 15));
  }

  const hours: number[] = [];
  for (let h = Math.ceil(viewStart / 60); h <= Math.floor(viewEnd / 60); h++) hours.push(h);

  const style = { "--type": typeColour(typeName) } as CSSProperties;

  return (
    <div className={`timeline${compact ? " compact" : ""}`} style={style} onClick={pick}
      title="Click a free time to start a booking there">
      {openHours === "closed" && <div className="closed" style={{ left: 0, width: "100%" }} />}
      {opens !== null && <div className="closed" style={{ left: 0, width: width(viewStart, opens) }} />}
      {closes !== null && <div className="closed" style={{ left: left(closes), width: width(closes, viewEnd) }} />}
      {hours.map((h) => (
        <span key={h}>
          <span className="tick" style={{ left: left(h * 60) }} />
          {!compact && <span className="hour" style={{ left: left(h * 60) }}>{String(h).padStart(2, "0")}</span>}
        </span>
      ))}
      {blocks.filter((b) => b.to > viewStart && b.from < viewEnd).map(({ booking, from, to }) => (
        <div key={booking.id} className="block" style={{ left: left(from), width: width(from, to) }}
          title={`${formatTime(booking.start, timeZone)} to ${formatTime(booking.end, timeZone)}, ${booking.ownerName}`}
          onClick={(e) => e.stopPropagation()}>
          {booking.ownerName}
        </div>
      ))}
      {hasSelection && selTo > selFrom && (
        <div className={`selection${clashes ? " clash" : ""}`} style={{ left: left(selFrom), width: width(selFrom, selTo) }} />
      )}
      {now !== null && now >= viewStart && now <= viewEnd && (
        <div className="now" style={{ left: left(now) }} title={`Now, ${fromMinutes(now)}`} />
      )}
    </div>
  );
}
