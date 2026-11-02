import { useEffect, useState } from "react";
import { api, describeError, Location, Paged, Resource, ResourceType } from "../../api";

const pageSize = 10;

// Members and admins: browse active resources, filtered by location and type.
export function FindResourcePage() {
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

  return (
    <section>
      <h2>Find a resource</h2>
      {errors.map((message) => (
        <p key={message} style={{ color: "#b00020" }}>{message}</p>
      ))}
      <p>
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
      </p>
      <table cellPadding={6}>
        <thead>
          <tr><th align="left">Name</th><th align="left">Type</th><th align="left">Location</th><th align="left">Capacity</th></tr>
        </thead>
        <tbody>
          {resources.map((r) => (
            <tr key={r.id}>
              <td>{r.name}</td>
              <td>{r.resourceTypeName}</td>
              <td>{r.locationName}</td>
              <td>{r.capacity ?? "-"}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {resources.length === 0 && <p>No resources match these filters.</p>}
      <p>
        <button disabled={page <= 1} onClick={() => load(page - 1)}>Previous</button>{" "}
        Page {page} of {lastPage} ({totalCount} resources){" "}
        <button disabled={page >= lastPage} onClick={() => load(page + 1)}>Next</button>
      </p>
    </section>
  );
}
