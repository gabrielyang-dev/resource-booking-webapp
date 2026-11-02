import { useEffect, useState } from "react";
import { api, describeError, Booking, Paged, Resource, User } from "../../api";
import { formatDate, formatTime } from "../../time";
import { StatusBadge } from "../../ui";

type ResourceUtilisation = {
  resourceId: string;
  resourceName: string;
  resourceTypeName: string;
  locationName: string;
  bookedHours: number;
  checkedInHours: number;
  noShowHours: number;
  openHours: number;
  utilisation: number;
};
type TypeUtilisation = {
  resourceTypeId: string; name: string; bookedHours: number; checkedInHours: number; noShowHours: number; openHours: number; utilisation: number;
};
type Utilisation = { from: string; to: string; resources: ResourceUtilisation[]; types: TypeUtilisation[] };

const statuses = ["Pending", "Confirmed", "CheckedIn", "Completed", "Cancelled", "Rejected", "Released"];
const pageSize = 20;

// Monday to Sunday of the week weeksAgo weeks back (0 = this week), as yyyy-mm-dd; spanWeeks long.
function weeks(weeksAgo: number, spanWeeks = 1): { from: string; to: string } {
  const today = new Date();
  const day = 24 * 60 * 60 * 1000;
  const monday = new Date(Date.UTC(today.getFullYear(), today.getMonth(), today.getDate() - ((today.getDay() + 6) % 7)) - weeksAgo * 7 * day);
  const sunday = new Date(monday.getTime() + (spanWeeks * 7 - 1) * day);
  return { from: monday.toISOString().slice(0, 10), to: sunday.toISOString().slice(0, 10) };
}

function percent(fraction: number): string {
  return `${Math.round(fraction * 100)}%`;
}

// A plain CSS bar: no chart library needed.
function Bar({ fraction }: { fraction: number }) {
  return (
    <span className="bar">
      <span style={{ width: `${Math.min(100, fraction * 100)}%` }} />
    </span>
  );
}

// Admin: how much each resource is used, and every booking ever made.
export function InsightsPage() {
  const [from, setFrom] = useState(weeks(1).from);
  const [to, setTo] = useState(weeks(1).to);
  const [utilisation, setUtilisation] = useState<Utilisation | null>(null);
  const [resources, setResources] = useState<Resource[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [resourceId, setResourceId] = useState("");
  const [userId, setUserId] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [history, setHistory] = useState<Paged<Booking> | null>(null);
  const [errors, setErrors] = useState<string[]>([]);

  useEffect(() => {
    async function loadLists() {
      try {
        setResources((await api<Paged<Resource>>("GET", "/api/resources?pageSize=100&includeInactive=true")).items);
        setUsers((await api<Paged<User>>("GET", "/api/users?pageSize=100")).items);
      } catch (error) {
        setErrors(describeError(error));
      }
    }
    loadLists();
  }, []);

  useEffect(() => {
    async function loadUtilisation() {
      try {
        setUtilisation(await api<Utilisation>("GET", `/api/insights/utilisation?from=${from}&to=${to}`));
      } catch (error) {
        setErrors(describeError(error));
      }
    }
    loadUtilisation();
  }, [from, to]);

  useEffect(() => {
    async function loadHistory() {
      const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
      if (resourceId) query.set("resourceId", resourceId);
      if (userId) query.set("userId", userId);
      if (status) query.set("status", status);
      try {
        setHistory(await api<Paged<Booking>>("GET", `/api/insights/history?${query}`));
      } catch (error) {
        setErrors(describeError(error));
      }
    }
    loadHistory();
  }, [resourceId, userId, status, page]);

  const lastPage = history ? Math.max(1, Math.ceil(history.totalCount / pageSize)) : 1;

  return (
    <section>
      <h2>Insights</h2>
      {errors.map((m) => <p key={m} className="error">{m}</p>)}

      <h3>Utilisation</h3>
      <p className="muted">Utilisation is booked hours divided by open hours (each resource's opening-hours rule, or 24 hours a day). Checked-in hours show what was really used; no-show hours were released because nobody turned up.</p>
      <div className="row" style={{ marginBottom: "0.75rem" }}>
        <label>From <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></label>
        <label>To <input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></label>
        <button onClick={() => { setFrom(weeks(0).from); setTo(weeks(0).to); }}>This week</button>
        <button onClick={() => { setFrom(weeks(1).from); setTo(weeks(1).to); }}>Last week</button>
        <button onClick={() => { setFrom(weeks(4, 4).from); setTo(weeks(1).to); }}>Last 4 weeks</button>
      </div>
      {utilisation && (
        <>
          <table cellPadding={6}>
            <thead>
              <tr><th align="left">Resource type</th><th align="right">Booked h</th><th align="right" title="Hours someone actually checked in to">Checked in h</th><th align="right" title="Bookings released because nobody checked in">No-show h</th><th align="right">Open h</th><th align="left">Utilisation</th></tr>
            </thead>
            <tbody>
              {utilisation.types.map((t) => (
                <tr key={t.resourceTypeId}>
                  <td><strong>{t.name}</strong></td>
                  <td align="right">{t.bookedHours}</td>
                  <td align="right">{t.checkedInHours}</td>
                  <td align="right">{t.noShowHours}</td>
                  <td align="right">{t.openHours}</td>
                  <td><Bar fraction={t.utilisation} /> {percent(t.utilisation)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <table cellPadding={6}>
            <thead>
              <tr><th align="left">Resource</th><th align="left">Type</th><th align="right">Booked h</th><th align="right" title="Hours someone actually checked in to">Checked in h</th><th align="right" title="Bookings released because nobody checked in">No-show h</th><th align="right">Open h</th><th align="left">Utilisation</th></tr>
            </thead>
            <tbody>
              {utilisation.resources.map((r) => (
                <tr key={r.resourceId}>
                  <td>{r.resourceName}</td>
                  <td>{r.resourceTypeName}</td>
                  <td align="right">{r.bookedHours}</td>
                  <td align="right">{r.checkedInHours}</td>
                  <td align="right">{r.noShowHours}</td>
                  <td align="right">{r.openHours}</td>
                  <td><Bar fraction={r.utilisation} /> {percent(r.utilisation)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}

      <h3>History</h3>
      <p>
        <select value={resourceId} onChange={(e) => { setResourceId(e.target.value); setPage(1); }}>
          <option value="">All resources</option>
          {resources.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
        </select>{" "}
        <select value={userId} onChange={(e) => { setUserId(e.target.value); setPage(1); }}>
          <option value="">All users</option>
          {users.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
        </select>{" "}
        <select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}>
          <option value="">All statuses</option>
          {statuses.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
      </p>
      {history && (
        <>
          <table cellPadding={6}>
            <thead>
              <tr><th align="left">Date</th><th align="left">Time</th><th align="left">Resource</th><th align="left">Booked by</th><th align="left">Status</th></tr>
            </thead>
            <tbody>
              {history.items.map((b) => (
                <tr key={b.id}>
                  <td>{formatDate(b.start, b.timeZone)}</td>
                  <td>{formatTime(b.start, b.timeZone)} to {formatTime(b.end, b.timeZone)}</td>
                  <td>{b.resourceName}</td>
                  <td>{b.ownerName}</td>
                  <td><StatusBadge status={b.status} /></td>
                </tr>
              ))}
            </tbody>
          </table>
          <p>
            <button disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</button>{" "}
            Page {page} of {lastPage} ({history.totalCount} bookings){" "}
            <button disabled={page >= lastPage} onClick={() => setPage(page + 1)}>Next</button>
          </p>
        </>
      )}
    </section>
  );
}
