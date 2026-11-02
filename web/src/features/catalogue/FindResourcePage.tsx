import { FormEvent, useEffect, useState } from "react";
import { api, describeError, Location, Paged, Resource, ResourceType } from "../../api";
import { localToIso, todayIn, weekdayFrom } from "../../time";
import { Messages } from "../../ui";
import { ResourcePage, Wanted } from "../bookings/ResourcePage";

const pageSize = 12;

type Intent = { resourceTypeId: string | null; resourceTypeName: string | null; date: string; start: string; end: string; attendees: number | null };

// Members and admins: browse resources by location and type, or ask what's free at a time.
// The time can be typed as a sentence when AI is switched on; it only fills the form.
export function FindResourcePage() {
  const [selected, setSelected] = useState<Resource | null>(null);
  const [locations, setLocations] = useState<Location[]>([]);
  const [types, setTypes] = useState<ResourceType[]>([]);
  const [locationId, setLocationId] = useState("");
  const [typeId, setTypeId] = useState("");
  const [resources, setResources] = useState<Resource[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [errors, setErrors] = useState<string[]>([]);
  const [notice, setNotice] = useState("");

  // The time search. When "searching" is on, the list shows only what's free and allowed then.
  const [wanted, setWanted] = useState<Wanted>({ date: weekdayFrom(todayIn("Europe/London")), start: "09:00", end: "10:00", attendees: "" });
  const [searching, setSearching] = useState(false);
  const [aiEnabled, setAiEnabled] = useState(false);
  const [sentence, setSentence] = useState("");

  useEffect(() => {
    async function loadFilters() {
      try {
        setLocations((await api<Paged<Location>>("GET", "/api/locations?pageSize=100")).items);
        setTypes((await api<Paged<ResourceType>>("GET", "/api/resource-types?pageSize=100")).items);
        setAiEnabled((await api<{ enabled: boolean }>("GET", "/api/booking-intents/enabled")).enabled);
      } catch (error) {
        setErrors(describeError(error));
      }
    }
    loadFilters();
  }, []);

  useEffect(() => {
    load(1);
  }, [locationId, typeId, searching]);

  // The location searched in: the chosen one, or the first (its time zone reads the times).
  const location = locations.find((l) => l.id === locationId) ?? locations[0];

  async function load(pageToLoad: number, search = wanted) {
    const query = new URLSearchParams({ page: String(pageToLoad), pageSize: String(pageSize) });
    if (locationId) query.set("locationId", locationId);
    if (typeId) query.set("resourceTypeId", typeId);
    try {
      let result: Paged<Resource>;
      if (searching && location) {
        query.set("start", localToIso(search.date, search.start, location.timeZone));
        query.set("end", localToIso(search.date, search.end, location.timeZone));
        if (search.attendees) query.set("attendees", search.attendees);
        result = await api<Paged<Resource>>("GET", `/api/availability?${query}`);
      } else {
        result = await api<Paged<Resource>>("GET", `/api/resources?${query}`);
      }
      setResources(result.items);
      setTotalCount(result.totalCount);
      setPage(pageToLoad);
      setErrors([]);
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  function search(event: FormEvent) {
    event.preventDefault();
    setNotice("");
    if (searching) load(1);
    else setSearching(true);
  }

  // AI fills the form; the user checks it, and the search (and later the rules) decide.
  async function understand(event: FormEvent) {
    event.preventDefault();
    if (!location) return;
    setErrors([]);
    setNotice("");
    try {
      const intent = await api<Intent>("POST", "/api/booking-intents", { text: sentence, locationId: location.id });
      const next = { date: intent.date, start: intent.start, end: intent.end, attendees: intent.attendees ? String(intent.attendees) : "" };
      setWanted(next);
      setTypeId(intent.resourceTypeId ?? "");
      setNotice(`Filled in: ${intent.resourceTypeName ?? "any type"}, ${intent.date}, ${intent.start} to ${intent.end}${intent.attendees ? `, ${intent.attendees} people` : ""}. Check it, then press Show what's free.`);
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  const lastPage = Math.max(1, Math.ceil(totalCount / pageSize));

  if (selected) {
    return <ResourcePage resource={selected} wanted={searching ? wanted : undefined} onBack={() => setSelected(null)} />;
  }

  return (
    <section>
      <h2>Find a resource</h2>

      {aiEnabled && (
        <form onSubmit={understand} className="card row">
          <label style={{ flex: 1 }}>Describe what you need
            <input value={sentence} onChange={(e) => setSentence(e.target.value)} placeholder="a room for 6 tomorrow at 2 for an hour" />
          </label>
          <button type="submit" className="primary">Fill in the form</button>
        </form>
      )}

      <form onSubmit={search} className="card">
        <div className="row">
          <label>Location
            <select value={locationId} onChange={(e) => setLocationId(e.target.value)}>
              <option value="">All locations</option>
              {locations.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
            </select>
          </label>
          <label>Type
            <select value={typeId} onChange={(e) => setTypeId(e.target.value)}>
              <option value="">All types</option>
              {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </label>
          <label>Day <input type="date" value={wanted.date} onChange={(e) => setWanted({ ...wanted, date: e.target.value })} required /></label>
          <label>From <input type="time" step={900} value={wanted.start} onChange={(e) => setWanted({ ...wanted, start: e.target.value })} required /></label>
          <label>To <input type="time" step={900} value={wanted.end} onChange={(e) => setWanted({ ...wanted, end: e.target.value })} required /></label>
          <label>People <input type="number" min={1} value={wanted.attendees} placeholder="any" style={{ width: "5rem" }}
            onChange={(e) => setWanted({ ...wanted, attendees: e.target.value })} /></label>
          <button type="submit">Show what's free</button>
          {searching && <button type="button" onClick={() => setSearching(false)}>Show everything</button>}
        </div>
      </form>

      {notice && <div className="notice">{notice}</div>}
      <Messages errors={errors} />
      <p className="muted">
        {searching
          ? `Free and allowed on ${wanted.date}, ${wanted.start} to ${wanted.end}${location ? ` (${location.timeZone})` : ""}. Pick one to book it.`
          : "All resources. Pick one to see its day and book it, or search a time to see only what's free."}
      </p>

      <div className="grid">
        {resources.map((r) => (
          <div key={r.id} className="card">
            <strong>{r.name}</strong>
            <span className="muted">{r.resourceTypeName} · {r.locationName}</span>
            <span className="muted">{r.capacity ? `Up to ${r.capacity} ${r.capacity === 1 ? "person" : "people"}` : "No people limit"}</span>
            <button className="primary" onClick={() => setSelected(r)}>{searching ? `Book ${wanted.start} to ${wanted.end}` : "See the day and book"}</button>
          </div>
        ))}
      </div>
      {resources.length === 0 && (
        <p className="empty">{searching ? "Nothing is free and allowed then. Try another time, or fewer people." : "No resources match these filters. Try another location or type."}</p>
      )}
      {lastPage > 1 && (
        <p>
          <button disabled={page <= 1} onClick={() => load(page - 1)}>Previous</button>{" "}
          Page {page} of {lastPage}{" "}
          <button disabled={page >= lastPage} onClick={() => load(page + 1)}>Next</button>
        </p>
      )}
    </section>
  );
}
