import { useEffect, useState } from "react";
import { api, describeError, Paged, PickerUser, User } from "../../api";

type Props = { onSignedIn: (user: User) => void };

export function SignIn({ onSignedIn }: Props) {
  const [users, setUsers] = useState<PickerUser[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [errors, setErrors] = useState<string[]>([]);
  const pageSize = 20;

  useEffect(() => {
    async function load() {
      try {
        const result = await api<Paged<PickerUser>>("GET", `/api/session/users?page=${page}&pageSize=${pageSize}`);
        setUsers(result.items);
        setTotalCount(result.totalCount);
      } catch (error) {
        setErrors(describeError(error));
      }
    }
    load();
  }, [page]);

  async function pick(id: string) {
    try {
      const user = await api<User>("POST", "/api/session", { userId: id });
      onSignedIn(user);
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  const lastPage = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <section>
      <h2>Sign in</h2>
      <p>Pick who you want to be. There are no passwords in this demo.</p>
      {errors.map((message) => (
        <p key={message} className="error">{message}</p>
      ))}
      <div className="grid">
        {users.map((user) => (
          <div key={user.id} className="card">
            <strong>{user.name}</strong>
            <span className="muted">{user.role === "Admin" ? "Admin: manages the catalogue, rules and insights" : "Member: books resources"}</span>
            <button className="primary" onClick={() => pick(user.id)}>Sign in as {user.name.split(" ")[0]}</button>
          </div>
        ))}
      </div>
      {lastPage > 1 && (
        <p>
          <button disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</button>{" "}
          Page {page} of {lastPage}{" "}
          <button disabled={page >= lastPage} onClick={() => setPage(page + 1)}>Next</button>
        </p>
      )}
    </section>
  );
}
