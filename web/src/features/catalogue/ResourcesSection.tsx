import { FormEvent, useEffect, useState } from "react";
import { api, describeError, Location, Paged, Resource, ResourceType } from "../../api";

const pageSize = 5;

// editing: null = form closed, "new" = add form, otherwise the resource being edited.
type Editing = null | "new" | Resource;

export function ResourcesSection() {
  const [items, setItems] = useState<Resource[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);
  const [types, setTypes] = useState<ResourceType[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [editing, setEditing] = useState<Editing>(null);
  const [errors, setErrors] = useState<string[]>([]);

  async function load(pageToLoad: number) {
    try {
      const result = await api<Paged<Resource>>(
        "GET", `/api/resources?includeInactive=true&page=${pageToLoad}&pageSize=${pageSize}`);
      setItems(result.items);
      setTotalCount(result.totalCount);
      setPage(pageToLoad);
      setErrors([]);
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  useEffect(() => {
    async function loadLists() {
      try {
        setLocations((await api<Paged<Location>>("GET", "/api/locations?pageSize=100")).items);
        setTypes((await api<Paged<ResourceType>>("GET", "/api/resource-types?pageSize=100")).items);
      } catch (error) {
        setErrors(describeError(error));
      }
    }
    loadLists();
    load(1);
  }, []);

  async function setActive(resource: Resource, active: boolean) {
    try {
      await api("PUT", `/api/resources/${resource.id}`, {
        locationId: resource.locationId,
        resourceTypeId: resource.resourceTypeId,
        name: resource.name,
        capacity: resource.capacity,
        active,
      });
      await load(page);
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  const lastPage = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <div>
      <h3>Resources</h3>
      {errors.map((message) => (
        <p key={message} className="error">{message}</p>
      ))}
      <table cellPadding={6}>
        <thead>
          <tr>
            <th align="left">Name</th><th align="left">Type</th><th align="left">Location</th>
            <th align="left">Capacity</th><th align="left">Status</th><th></th>
          </tr>
        </thead>
        <tbody>
          {items.map((r) => (
            <tr key={r.id} style={{ color: r.active ? undefined : "#777" }}>
              <td>{r.name}</td>
              <td>{r.resourceTypeName}</td>
              <td>{r.locationName}</td>
              <td>{r.capacity ?? "-"}</td>
              <td>{r.active ? "Active" : "Inactive"}</td>
              <td>
                <button onClick={() => setEditing(r)}>Edit</button>{" "}
                <button onClick={() => setActive(r, !r.active)}>{r.active ? "Deactivate" : "Activate"}</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <p>
        <button disabled={page <= 1} onClick={() => load(page - 1)}>Previous</button>{" "}
        Page {page} of {lastPage} ({totalCount} resources){" "}
        <button disabled={page >= lastPage} onClick={() => load(page + 1)}>Next</button>
      </p>
      {editing === null ? (
        <button onClick={() => setEditing("new")}>Add resource</button>
      ) : (
        <ResourceForm
          key={editing === "new" ? "new" : editing.id}
          resource={editing === "new" ? null : editing}
          locations={locations}
          types={types}
          onCancel={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            load(page);
          }}
        />
      )}
    </div>
  );
}

type FormProps = {
  resource: Resource | null;
  locations: Location[];
  types: ResourceType[];
  onCancel: () => void;
  onSaved: () => void;
};

function ResourceForm({ resource, locations, types, onCancel, onSaved }: FormProps) {
  const [name, setName] = useState(resource?.name ?? "");
  const [locationId, setLocationId] = useState(resource?.locationId ?? locations[0]?.id ?? "");
  const [typeId, setTypeId] = useState(resource?.resourceTypeId ?? types[0]?.id ?? "");
  const [capacity, setCapacity] = useState(resource?.capacity?.toString() ?? "");
  const [errors, setErrors] = useState<string[]>([]);

  async function save(event: FormEvent) {
    event.preventDefault();
    // Empty means "not a limit on people". Anything else must be a whole number of 1 or more,
    // so a typo is never saved as "no limit" by accident.
    const typed = capacity.trim();
    if (typed !== "" && (!/^[0-9]{1,5}$/.test(typed) || Number(typed) < 1 || Number(typed) > 10000)) {
      setErrors(["Capacity must be a whole number from 1 to 10,000, or left empty if it is not a limit on people."]);
      return;
    }
    try {
      const body = {
        locationId: locationId || null,
        resourceTypeId: typeId || null,
        name,
        capacity: typed === "" ? null : Number(typed),
        active: resource?.active ?? true,
      };
      if (resource === null) {
        await api("POST", "/api/resources", body);
      } else {
        await api("PUT", `/api/resources/${resource.id}`, body);
      }
      onSaved();
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  return (
    <form onSubmit={save} style={{ border: "1px solid #ccc", padding: "1rem", maxWidth: "28rem" }}>
      <h4>{resource === null ? "Add resource" : `Edit ${resource.name}`}</h4>
      {errors.map((message) => (
        <p key={message} className="error">{message}</p>
      ))}
      <p>
        <label>Name<br /><input value={name} onChange={(e) => setName(e.target.value)} /></label>
      </p>
      <p>
        <label>Location<br />
          <select value={locationId} onChange={(e) => setLocationId(e.target.value)}>
            {locations.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
          </select>
        </label>
      </p>
      <p>
        <label>Type<br />
          <select value={typeId} onChange={(e) => setTypeId(e.target.value)}>
            {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
        </label>
      </p>
      <p>
        <label>Capacity<br /><input value={capacity} onChange={(e) => setCapacity(e.target.value)} /></label>
        <br /><small>Whole number of people. Leave empty if it is not a limit on people.</small>
      </p>
      <button type="submit">Save</button>{" "}
      <button type="button" onClick={onCancel}>Cancel</button>
    </form>
  );
}
