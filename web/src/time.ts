// Times are stored in UTC and shown in the location's IANA time zone.

// How far the zone is ahead of UTC at that moment, in milliseconds.
function offsetAt(utcMs: number, timeZone: string): number {
  const parts = new Intl.DateTimeFormat("en-GB", {
    timeZone,
    hourCycle: "h23",
    year: "numeric",
    month: "numeric",
    day: "numeric",
    hour: "numeric",
    minute: "numeric",
    second: "numeric",
  }).formatToParts(new Date(utcMs));
  const get = (type: string) => Number(parts.find((p) => p.type === type)!.value);
  const asIfUtc = Date.UTC(get("year"), get("month") - 1, get("day"), get("hour"), get("minute"), get("second"));
  return asIfUtc - Math.floor(utcMs / 1000) * 1000;
}

// "2030-06-10" + "10:00" on a clock in Europe/London -> the UTC instant as an ISO string.
export function localToIso(date: string, time: string, timeZone: string): string {
  const [year, month, day] = date.split("-").map(Number);
  const [hour, minute] = time.split(":").map(Number);
  const wallClockAsUtc = Date.UTC(year, month - 1, day, hour, minute);
  // Two passes so a time just after a clock change still lands on the right offset.
  let utcMs = wallClockAsUtc - offsetAt(wallClockAsUtc, timeZone);
  utcMs = wallClockAsUtc - offsetAt(utcMs, timeZone);
  return new Date(utcMs).toISOString();
}

// The UTC range [from, to) covering one local day.
export function dayRange(date: string, timeZone: string): { from: string; to: string } {
  const next = new Date(Date.parse(date + "T00:00:00Z") + 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
  return { from: localToIso(date, "00:00", timeZone), to: localToIso(next, "00:00", timeZone) };
}

export function formatTime(iso: string, timeZone: string): string {
  return new Date(iso).toLocaleTimeString("en-GB", { timeZone, hour: "2-digit", minute: "2-digit", hourCycle: "h23" });
}

export function formatDate(iso: string, timeZone: string): string {
  return new Date(iso).toLocaleDateString("en-GB", { timeZone, weekday: "short", day: "numeric", month: "short", year: "numeric" });
}

// Today's date on the clock in the zone, as yyyy-mm-dd.
export function todayIn(timeZone: string): string {
  return new Date().toLocaleDateString("en-CA", { timeZone });
}

// "09:30" -> 570 minutes after midnight, and back.
export function toMinutes(time: string): number {
  const [hour, minute] = time.split(":").map(Number);
  return hour * 60 + minute;
}

export function fromMinutes(minutes: number): string {
  const clamped = Math.max(0, Math.min(24 * 60 - 1, minutes));
  return `${String(Math.floor(clamped / 60)).padStart(2, "0")}:${String(clamped % 60).padStart(2, "0")}`;
}

// The local yyyy-mm-dd of a UTC instant in the zone.
export function localDate(iso: string, timeZone: string): string {
  return new Date(iso).toLocaleDateString("en-CA", { timeZone });
}

// "Mon".."Sun" for a yyyy-mm-dd date, matching the rule settings.
export function dayName(date: string): string {
  return ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"][new Date(date + "T12:00:00Z").getUTCDay()];
}

// The date itself on a weekday, otherwise the following Monday.
export function weekdayFrom(date: string): string {
  let d = new Date(date + "T12:00:00Z");
  while (d.getUTCDay() === 0 || d.getUTCDay() === 6) {
    d = new Date(d.getTime() + 24 * 60 * 60 * 1000);
  }
  return d.toISOString().slice(0, 10);
}
