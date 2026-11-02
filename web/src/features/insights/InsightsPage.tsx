import { useEffect, useState } from "react";
import { api, describeError, Booking, Paged, Resource, User } from "../../api";
import { formatDate, formatTime } from "../../time";

type ResourceUtilisation = {
  resourceId: string;
  resourceName: string;
  resourceTypeName: string;
  locationName: string;
  bookedHours: number;
  openHours: number;
  utilisation: number;
};
type TypeUtilisation = { resourceTypeId: string; name: string; bookedHours: number; openHours: number; utilisation: number };
type Utilisation = { from: string; to: string; resources: ResourceUtilisation[]; types: TypeUtilisation[] };

const statuses = ["Pending", "Confirmed", "CheckedIn", "Completed", "Cancelled", "Rejected", "Released"];
const pageSize = 20;

// Monday and Sunday of the current week, as yyyy-mm-dd.
function thisWeek(): { from: string; to: string } {
  const today = new Date();
  const monday = new Date(Date.UTC(today.getFullYear(), today.getMonth(), today.getDate() - ((today.getDay() + 6) % 7)));
  const sunday = new Date(monday.getTime() + 6 * 24 * 60 * 60 * 1000);
  return { from: monday.toISOString().slice(0, 10), to: sunday.toISOString().slice(0, 10) };
}

function percent(fraction: number): string {
  return `${Math.round(fraction * 100)}%`;
}

// A plain CSS bar: no chart library needed.
function Bar({ fraction }: { fraction: number }) {
  return (
    <div style={{ width: 160, height: 12, background: "#e5e5e5" }}>
      <div style={{ width: `${Math.min(100, fraction * 100)}%`, height: "100%", background: "#2f6fdf" }} />
    </div>
  );
}

// Admin: how much each resource is used, and every booking ever made.
export function InsightsPage() {
  const [from, setFrom] = useState(thisWeek().from);
  const [to, setTo] = useState(thisWeek().to);
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
      {errors.map((m) => <p key={m} style={{ color: "#b00020" }}>{m}</p>)}

      <h3>Utilisation</h3>
      <p>Booked hours divided by open hours. Open hours follow each resource's opening-hours rule, or 24 hours a day if it has none.</p>
      <p>
        <label>From <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></label>{" "}
        <label>To <input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></label>
      </p>
      {utilisation && (
        <>
          <table cellPadding={6}>
            <thead>
              <tr><th align="left">Resource type</th><th align="right">Booked h</th><th align="right">Open h</th><th align="left">Utilisation</th></tr>
            </thead>
            <tbody>
              {utilisation.types.map((t) => (
                <tr key={t.resourceTypeId}>
                  <td><strong>{t.name}</strong></td>
                  <td align="right">{t.bookedHours}</td>
                  <td align="right">{t.openHours}</td>
                  <td><Bar fraction={t.utilisation} /> {percent(t.utilisation)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <table cellPadding={6}>
            <thead>
              <tr><th align="left">Resource</th><th align="left">Type</th><th align="right">Booked h</th><th align="right">Open h</th><th align="left">Utilisation</th></tr>
            </thead>
            <tbody>
              {utilisation.resources.map((r) => (
                <tr key={r.resourceId}>
                  <td>{r.resourceName}</td>
                  <td>{r.resourceTypeName}</td>
                  <td align="right">{r.bookedHours}</td>
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
                  <td>{b.status}</td>
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
