import { CSSProperties, useEffect, useState } from "react";
import { api, describeError, Location, Paged, Resource, ResourceBooking } from "../../api";
import { dayName, dayRange, formatDate, fromMinutes, localToIso, todayIn, toMinutes, weekdayFrom } from "../../time";
import { Messages, typeColour } from "../../ui";
import { DayTimeline, OpenHours, viewFor } from "./DayTimeline";
import { ResourcePage, Wanted } from "./ResourcePage";

type Rule = { kind: string; settings: { opens?: string; closes?: string; days?: string[] } };

// One row of the board: a resource, its bookings that day and its opening hours.
type Row = { resource: Resource; bookings: ResourceBooking[]; hoursRule: Rule["settings"] | undefined };

// Home: every bookable space in the office on one day, stacked like a building directory.
// Click a gap on any row to book that resource from there.
export function OfficeTodayPage() {
  const [location, setLocation] = useState<Location | null>(null);
  const [date, setDate] = useState(weekdayFrom(todayIn("Europe/London")));
  const [rows, setRows] = useState<Row[]>([]);
  const [errors, setErrors] = useState<string[]>([]);
  const [selected, setSelected] = useState<{ resource: Resource; wanted?: Wanted } | null>(null);

  useEffect(() => {
    api<Paged<Location>>("GET", "/api/locations?pageSize=1")
      .then((page) => setLocation(page.items[0] ?? null))
      .catch((error) => setErrors(describeError(error)));
  }, []);

  useEffect(() => {
    if (location) load(location);
  }, [location, date]);

  // One request for the resources, then their day and rules side by side (a handful per office).
  async function load(at: Location) {
    try {
      const resources = (await api<Paged<Resource>>("GET", `/api/resources?locationId=${at.id}&pageSize=100`)).items;
      const { from, to } = dayRange(date, at.timeZone);
      const loaded = await Promise.all(resources.map(async (resource) => {
        const [bookings, rules] = await Promise.all([
          api<ResourceBooking[]>("GET", `/api/resources/${resource.id}/bookings?from=${from}&to=${to}`),
          api<Rule[]>("GET", `/api/resources/${resource.id}/rules`),
        ]);
        return { resource, bookings, hoursRule: rules.find((r) => r.kind === "OpeningHours")?.settings };
      }));
      // Grouped by kind of space, then by name, so each colour sits together.
      loaded.sort((a, b) => a.resource.resourceTypeName.localeCompare(b.resource.resourceTypeName)
        || a.resource.name.localeCompare(b.resource.name));
      setRows(loaded);
      setErrors([]);
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  function openHoursOf(row: Row): OpenHours {
    if (!row.hoursRule) return null;
    if (row.hoursRule.days && !row.hoursRule.days.includes(dayName(date))) return "closed";
    return { opens: row.hoursRule.opens ?? "00:00", closes: row.hoursRule.closes ?? "23:59" };
  }

  if (selected) {
    return <ResourcePage resource={selected.resource} wanted={selected.wanted} onBack={() => { setSelected(null); if (location) load(location); }} />;
  }

  // Every row shares one span, so the hours line up down the board.
  const spans = rows.map((row) => viewFor(openHoursOf(row)));
  const viewStart = spans.length ? Math.min(...spans.map((s) => s.start)) : 7 * 60;
  const viewEnd = spans.length ? Math.max(...spans.map((s) => s.end)) : 20 * 60;
  const hours: number[] = [];
  for (let h = Math.ceil(viewStart / 60); h <= Math.floor(viewEnd / 60); h++) hours.push(h);

  function shiftDay(days: number) {
    const next = new Date(Date.parse(date + "T12:00:00Z") + days * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
    setDate(next);
  }

  const isToday = location && date === todayIn(location.timeZone);

  return (
    <section>
      <h2>{isToday ? "The office today" : `The office on ${formatDate(localToIso(date, "12:00", location?.timeZone ?? "Europe/London"), location?.timeZone ?? "Europe/London")}`}</h2>
      <p className="subtitle">
        Every bookable space{location ? ` in ${location.name}` : ""}, hour by hour. Click a gap on any row to book it from that time.
      </p>
      <div className="row" style={{ marginBottom: "1.5rem" }}>
        <button onClick={() => shiftDay(-1)}>Previous day</button>
        <label>Day <input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></label>
        <button onClick={() => shiftDay(1)}>Next day</button>
        {location && !isToday && <button onClick={() => setDate(todayIn(location.timeZone))}>Today</button>}
      </div>
      <Messages errors={errors} />

      {rows.length === 0 ? <p className="empty">Loading the office...</p> : (
        <div className="board">
          <div />
          <div className="ruler" aria-hidden="true">
            {hours.map((h) => (
              <span key={h} style={{ left: `${((h * 60 - viewStart) / (viewEnd - viewStart)) * 100}%` }}>{String(h).padStart(2, "0")}</span>
            ))}
          </div>
          {rows.map((row) => (
            <BoardRow key={row.resource.id} row={row} date={date} openHours={openHoursOf(row)} viewStart={viewStart} viewEnd={viewEnd}
              onOpen={(wanted) => setSelected({ resource: row.resource, wanted })} />
          ))}
        </div>
      )}
    </section>
  );
}

type BoardRowProps = {
  row: Row;
  date: string;
  openHours: OpenHours;
  viewStart: number;
  viewEnd: number;
  onOpen: (wanted?: Wanted) => void;
};

function BoardRow({ row, date, openHours, viewStart, viewEnd, onOpen }: BoardRowProps) {
  const { resource } = row;
  const style = { "--type": typeColour(resource.resourceTypeName) } as CSSProperties;
  return (
    <>
      <div className="name" style={style}>
        <button onClick={() => onOpen()}>{resource.name}</button>
        <small>{resource.resourceTypeName}{resource.capacity ? `, ${resource.capacity} ${resource.capacity === 1 ? "person" : "people"}` : ""}</small>
      </div>
      <DayTimeline compact date={date} timeZone={resource.timeZone} bookings={row.bookings} openHours={openHours}
        typeName={resource.resourceTypeName} viewStart={viewStart} viewEnd={viewEnd}
        onPick={(start) => onOpen({ date, start, end: fromMinutes(toMinutes(start) + 60), attendees: "" })} />
    </>
  );
}
