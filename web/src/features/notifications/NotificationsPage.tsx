import { useEffect, useState } from "react";
import { api, describeError, Notification, Paged } from "../../api";
import { Messages } from "../../ui";

// The signed-in user's notifications, newest first. Opening the page marks them read.
export function NotificationsPage({ onRead }: { onRead: () => void }) {
  const [items, setItems] = useState<Notification[]>([]);
  const [errors, setErrors] = useState<string[]>([]);

  useEffect(() => {
    async function load() {
      try {
        setItems((await api<Paged<Notification>>("GET", "/api/notifications?pageSize=50")).items);
        await api("POST", "/api/notifications/read-all");
        onRead();
      } catch (error) {
        setErrors(describeError(error));
      }
    }
    load();
  }, []);

  return (
    <section>
      <h2>Notifications</h2>
      <Messages errors={errors} />
      {items.length === 0 ? <p className="empty">No notifications yet.</p> : (
        <div>
          {items.map((n) => (
            <div key={n.id} className="card" style={{ borderLeft: n.read ? undefined : "4px solid var(--brand)" }}>
              <div>{n.message}</div>
              <small className="muted">{new Date(n.createdAt).toLocaleString("en-GB", { dateStyle: "medium", timeStyle: "short" })}</small>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
