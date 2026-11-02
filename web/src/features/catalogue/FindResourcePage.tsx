import { useEffect, useState } from "react";
import { api, describeError, Location, Paged, Resource, ResourceType } from "../../api";
import { ResourcePage } from "../bookings/ResourcePage";

const pageSize = 10;

// Members and admins: browse active resources, filtered by location and type.
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

  useEffect(() => {
    async function loadFilters() {
      try {
        const locationPage = await api<Paged<Location>>("GET", "/api/locations?pageSize=100");
        const typePage = await api<Paged<ResourceType>>("GET", "/api/resource-types?pageSize=100");
        setLocations(locationPage.items);
        setTypes(typePage.items);
      } catch (error) {
        setErrors(describeError(error));
      }
    }
    loadFilters();
  }, []);

  useEffect(() => {
    load(1);
  }, [locationId, typeId]);

  async function load(pageToLoad: number) {
    const query = new URLSearchParams({ page: String(pageToLoad), pageSize: String(pageSize) });
    if (locationId) query.set("locationId", locationId);
    if (typeId) query.set("resourceTypeId", typeId);
    try {
      const result = await api<Paged<Resource>>("GET", `/api/resources?${query}`);
      setResources(result.items);
      setTotalCount(result.totalCount);
      setPage(pageToLoad);
      setErrors([]);
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  const lastPage = Math.max(1, Math.ceil(totalCount / pageSize));

  if (selected) {
    return <ResourcePage resource={selected} onBack={() => setSelected(null)} />;
  }

  return (
    <section>
      <h2>Find a resource</h2>
      <p className="muted">Pick a resource to see its day and book it.</p>
      {errors.map((message) => (
        <p key={message} className="error">{message}</p>
      ))}
      <div className="row" style={{ marginBottom: "1rem" }}>
        <label>Location{" "}
          <select value={locationId} onChange={(e) => setLocationId(e.target.value)}>
            <option value="">All locations</option>
            {locations.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
          </select>
        </label>{" "}
        <label>Type{" "}
          <select value={typeId} onChange={(e) => setTypeId(e.target.value)}>
            <option value="">All types</option>
            {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
        </label>
      </div>
      <div className="grid">
        {resources.map((r) => (
          <div key={r.id} className="card">
            <strong>{r.name}</strong>
            <span className="muted">{r.resourceTypeName} · {r.locationName}</span>
            <span className="muted">{r.capacity ? `Up to ${r.capacity} ${r.capacity === 1 ? "person" : "people"}` : "No people limit"}</span>
            <button className="primary" onClick={() => setSelected(r)}>See the day and book</button>
          </div>
        ))}
      </div>
      {resources.length === 0 && <p className="empty">No resources match these filters. Try another location or type.</p>}
      <p>
        <button disabled={page <= 1} onClick={() => load(page - 1)}>Previous</button>{" "}
        Page {page} of {lastPage} ({totalCount} resources){" "}
        <button disabled={page >= lastPage} onClick={() => load(page + 1)}>Next</button>
      </p>
    </section>
  );
}
