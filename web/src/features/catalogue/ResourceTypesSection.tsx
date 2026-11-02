import { FormEvent, useEffect, useState } from "react";
import { api, describeError, Paged, ResourceType } from "../../api";

const pageSize = 5;

// editing: null = form closed, "new" = add form, otherwise the type being edited.
type Editing = null | "new" | ResourceType;

export function ResourceTypesSection() {
  const [items, setItems] = useState<ResourceType[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [editing, setEditing] = useState<Editing>(null);
  const [errors, setErrors] = useState<string[]>([]);

  async function load(pageToLoad: number) {
    try {
      const result = await api<Paged<ResourceType>>("GET", `/api/resource-types?page=${pageToLoad}&pageSize=${pageSize}`);
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

  async function remove(type: ResourceType) {
    if (!window.confirm(`Delete ${type.name}?`)) {
      return;
    }
    try {
      await api("DELETE", `/api/resource-types/${type.id}`);
      await load(page);
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  const lastPage = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <div>
      <h3>Resource types</h3>
      {errors.map((message) => (
        <p key={message} style={{ color: "#b00020" }}>{message}</p>
      ))}
      <table cellPadding={6}>
        <thead>
          <tr><th align="left">Name</th><th></th></tr>
        </thead>
        <tbody>
          {items.map((t) => (
            <tr key={t.id}>
              <td>{t.name}</td>
              <td>
                <button onClick={() => setEditing(t)}>Edit</button>{" "}
                <button onClick={() => remove(t)}>Delete</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <p>
        <button disabled={page <= 1} onClick={() => load(page - 1)}>Previous</button>{" "}
        Page {page} of {lastPage} ({totalCount} resource types){" "}
        <button disabled={page >= lastPage} onClick={() => load(page + 1)}>Next</button>
      </p>
      {editing === null ? (
        <button onClick={() => setEditing("new")}>Add resource type</button>
      ) : (
        <TypeForm
          key={editing === "new" ? "new" : editing.id}
          type={editing === "new" ? null : editing}
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

type FormProps = { type: ResourceType | null; onCancel: () => void; onSaved: () => void };

function TypeForm({ type, onCancel, onSaved }: FormProps) {
  const [name, setName] = useState(type?.name ?? "");
  const [errors, setErrors] = useState<string[]>([]);

  async function save(event: FormEvent) {
    event.preventDefault();
    try {
      if (type === null) {
        await api("POST", "/api/resource-types", { name });
      } else {
        await api("PUT", `/api/resource-types/${type.id}`, { name });
      }
      onSaved();
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  return (
    <form onSubmit={save} style={{ border: "1px solid #ccc", padding: "1rem", maxWidth: "28rem" }}>
      <h4>{type === null ? "Add resource type" : `Edit ${type.name}`}</h4>
      {errors.map((message) => (
        <p key={message} style={{ color: "#b00020" }}>{message}</p>
      ))}
      <p>
        <label>Name<br /><input value={name} onChange={(e) => setName(e.target.value)} /></label>
      </p>
      <button type="submit">Save</button>{" "}
      <button type="button" onClick={onCancel}>Cancel</button>
    </form>
  );
}
