import { FormEvent, useEffect, useState } from "react";
import { api, describeError, Paged, Role, User } from "../../api";

const pageSize = 5;

// editing: null = form closed, "new" = add form, otherwise the user being edited.
type Editing = null | "new" | User;

export function UsersPage() {
  const [users, setUsers] = useState<User[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [editing, setEditing] = useState<Editing>(null);
  const [errors, setErrors] = useState<string[]>([]);

  async function load(pageToLoad: number) {
    try {
      const result = await api<Paged<User>>("GET", `/api/users?page=${pageToLoad}&pageSize=${pageSize}`);
      setUsers(result.items);
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

  const lastPage = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <section>
      <h2>Users</h2>
      {errors.map((message) => (
        <p key={message} style={{ color: "#b00020" }}>{message}</p>
      ))}
      <table cellPadding={6}>
        <thead>
          <tr><th align="left">Name</th><th align="left">Email</th><th align="left">Role</th><th></th></tr>
        </thead>
        <tbody>
          {users.map((user) => (
            <tr key={user.id}>
              <td>{user.name}</td>
              <td>{user.email}</td>
              <td>{user.role}</td>
              <td><button onClick={() => setEditing(user)}>Edit</button></td>
            </tr>
          ))}
        </tbody>
      </table>
      <p>
        <button disabled={page <= 1} onClick={() => load(page - 1)}>Previous</button>{" "}
        Page {page} of {lastPage} ({totalCount} users){" "}
        <button disabled={page >= lastPage} onClick={() => load(page + 1)}>Next</button>
      </p>
      {editing === null ? (
        <button onClick={() => setEditing("new")}>Add user</button>
      ) : (
        <UserForm
          // A new key resets the form fields when switching between users.
          key={editing === "new" ? "new" : editing.id}
          user={editing === "new" ? null : editing}
          onCancel={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            load(page);
          }}
        />
      )}
    </section>
  );
}

type FormProps = { user: User | null; onCancel: () => void; onSaved: () => void };

function UserForm({ user, onCancel, onSaved }: FormProps) {
  const [name, setName] = useState(user?.name ?? "");
  const [email, setEmail] = useState(user?.email ?? "");
  const [role, setRole] = useState<Role>(user?.role ?? "Member");
  const [errors, setErrors] = useState<string[]>([]);

  async function save(event: FormEvent) {
    event.preventDefault();
    try {
      const body = { name, email, role };
      if (user === null) {
        await api("POST", "/api/users", body);
      } else {
        await api("PUT", `/api/users/${user.id}`, body);
      }
      onSaved();
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  return (
    <form onSubmit={save} style={{ border: "1px solid #ccc", padding: "1rem", maxWidth: "24rem" }}>
      <h3>{user === null ? "Add user" : `Edit ${user.name}`}</h3>
      {errors.map((message) => (
        <p key={message} style={{ color: "#b00020" }}>{message}</p>
      ))}
      <p>
        <label>Name<br /><input value={name} onChange={(e) => setName(e.target.value)} /></label>
      </p>
      <p>
        <label>Email<br /><input value={email} onChange={(e) => setEmail(e.target.value)} /></label>
      </p>
      <p>
        <label>Role<br />
          <select value={role} onChange={(e) => setRole(e.target.value as Role)}>
            <option value="Member">Member</option>
            <option value="Admin">Admin</option>
          </select>
        </label>
      </p>
      <button type="submit">Save</button>{" "}
      <button type="button" onClick={onCancel}>Cancel</button>
    </form>
  );
}
