import { useEffect, useState } from "react";
import { api, describeError, Booking, Paged } from "../../api";
import { formatDate, formatTime } from "../../time";
import { Messages } from "../../ui";

// Bookings waiting for the signed-in user's decision, on the resources they approve.
export function ApprovalsPage() {
  const [waiting, setWaiting] = useState<Booking[]>([]);
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [errors, setErrors] = useState<string[]>([]);
  const [message, setMessage] = useState("");

  useEffect(() => {
    load();
  }, []);

  async function load() {
    try {
      setWaiting((await api<Paged<Booking>>("GET", "/api/bookings/approvals?pageSize=100")).items);
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  function describe(b: Booking) {
    return `${b.resourceName} for ${b.ownerName}, ${formatDate(b.start, b.timeZone)} ${formatTime(b.start, b.timeZone)} to ${formatTime(b.end, b.timeZone)}`;
  }

  async function decide(b: Booking, approve: boolean) {
    setErrors([]);
    setMessage("");
    try {
      if (approve) {
        await api("POST", `/api/bookings/${b.id}/approve`);
        setMessage(`Approved: ${describe(b)}. ${b.ownerName} has been told.`);
      } else {
        await api("POST", `/api/bookings/${b.id}/reject`, { reason: reasons[b.id] ?? "" });
        setMessage(`Rejected: ${describe(b)}. ${b.ownerName} has been told why.`);
      }
      await load();
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  return (
    <section>
      <h2>Approvals</h2>
      <p className="subtitle">Some resources, like the pool car, need someone to say yes. Requests still hold the slot while they wait, and expire if nobody decides before they start.</p>
      <Messages errors={errors} success={message} />
      {waiting.length === 0 ? <p className="empty">Nothing is waiting for you.</p> : (
        <table>
          <thead><tr><th>Resource</th><th>Requested by</th><th>When</th><th>Decision</th></tr></thead>
          <tbody>
            {waiting.map((b) => (
              <tr key={b.id}>
                <td>{b.resourceName}</td>
                <td>{b.ownerName}</td>
                <td>{formatDate(b.start, b.timeZone)}, {formatTime(b.start, b.timeZone)} to {formatTime(b.end, b.timeZone)}</td>
                <td>
                  <div className="row">
                    <button className="primary" onClick={() => decide(b, true)}>Approve</button>
                    <input placeholder="Reason, if you reject" value={reasons[b.id] ?? ""}
                      onChange={(e) => setReasons({ ...reasons, [b.id]: e.target.value })} />
                    <button className="danger" onClick={() => decide(b, false)}>Reject</button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
