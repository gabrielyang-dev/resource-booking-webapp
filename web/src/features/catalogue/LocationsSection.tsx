import { FormEvent, useEffect, useState } from "react";
import { api, describeError, Location, Paged } from "../../api";

const pageSize = 5;

// editing: null = form closed, "new" = add form, otherwise the location being edited.
type Editing = null | "new" | Location;

export function LocationsSection() {
  const [items, setItems] = useState<Location[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [editing, setEditing] = useState<Editing>(null);
  const [errors, setErrors] = useState<string[]>([]);

  async function load(pageToLoad: number) {
    try {
      const result = await api<Paged<Location>>("GET", `/api/locations?page=${pageToLoad}&pageSize=${pageSize}`);
      if (result.items.length === 0 && pageToLoad > 1) {
        return load(pageToLoad - 1);
      }
      setItems(result.items);
      setTotalCount(result.totalCount);
      setPage(pageToLoad);
      setErrors([]);
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  useEffect(() => {
    load(1);
  }, []);

  async function remove(location: Location) {
    if (!window.confirm(`Delete ${location.name}?`)) {
      return;
    }
    try {
      await api("DELETE", `/api/locations/${location.id}`);
      await load(page);
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  const lastPage = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <div>
      <h3>Locations</h3>
      {errors.map((message) => (
        <p key={message} style={{ color: "#b00020" }}>{message}</p>
      ))}
      <table cellPadding={6}>
        <thead>
          <tr><th align="left">Name</th><th align="left">Time zone</th><th></th></tr>
        </thead>
        <tbody>
          {items.map((l) => (
            <tr key={l.id}>
              <td>{l.name}</td>
              <td>{l.timeZone}</td>
              <td>
                <button onClick={() => setEditing(l)}>Edit</button>{" "}
                <button onClick={() => remove(l)}>Delete</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <p>
        <button disabled={page <= 1} onClick={() => load(page - 1)}>Previous</button>{" "}
        Page {page} of {lastPage} ({totalCount} locations){" "}
        <button disabled={page >= lastPage} onClick={() => load(page + 1)}>Next</button>
      </p>
      {editing === null ? (
        <button onClick={() => setEditing("new")}>Add location</button>
      ) : (
        <LocationForm
          key={editing === "new" ? "new" : editing.id}
          location={editing === "new" ? null : editing}
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

type FormProps = { location: Location | null; onCancel: () => void; onSaved: () => void };

function LocationForm({ location, onCancel, onSaved }: FormProps) {
  const [name, setName] = useState(location?.name ?? "");
  const [timeZone, setTimeZone] = useState(location?.timeZone ?? "");
  const [errors, setErrors] = useState<string[]>([]);

  async function save(event: FormEvent) {
    event.preventDefault();
    try {
      const body = { name, timeZone };
      if (location === null) {
        await api("POST", "/api/locations", body);
      } else {
        await api("PUT", `/api/locations/${location.id}`, body);
      }
      onSaved();
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  return (
    <form onSubmit={save} style={{ border: "1px solid #ccc", padding: "1rem", maxWidth: "28rem" }}>
      <h4>{location === null ? "Add location" : `Edit ${location.name}`}</h4>
      {errors.map((message) => (
        <p key={message} style={{ color: "#b00020" }}>{message}</p>
      ))}
      <p>
        <label>Name<br /><input value={name} onChange={(e) => setName(e.target.value)} /></label>
      </p>
      <p>
        <label>Time zone<br />
          <input value={timeZone} onChange={(e) => setTimeZone(e.target.value)} placeholder="Europe/London" />
        </label>
        <br /><small>An IANA name such as Europe/London or America/New_York.</small>
      </p>
      <button type="submit">Save</button>{" "}
      <button type="button" onClick={onCancel}>Cancel</button>
    </form>
  );
}
