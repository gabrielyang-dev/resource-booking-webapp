import { FormEvent, useEffect, useState } from "react";
import { api, describeError, AddOn, Location, Paged } from "../../api";
import { Messages } from "../../ui";

type Form = { id: string | null; locationId: string; name: string; leadHours: string; from: string; until: string; active: boolean };

const empty: Form = { id: null, locationId: "", name: "", leadHours: "24", from: "08:00", until: "15:00", active: true };

// Admin: things ordered with a booking (catering, AV). Deactivated, never deleted.
export function AddOnsSection() {
  const [addOns, setAddOns] = useState<AddOn[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);
  const [form, setForm] = useState<Form>(empty);
  const [errors, setErrors] = useState<string[]>([]);
  const [message, setMessage] = useState("");

  useEffect(() => {
    load();
    api<Paged<Location>>("GET", "/api/locations?pageSize=100").then((p) => setLocations(p.items)).catch((e) => setErrors(describeError(e)));
  }, []);

  async function load() {
    try {
      setAddOns((await api<Paged<AddOn>>("GET", "/api/add-ons?includeInactive=true&pageSize=100")).items);
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  function edit(a: AddOn) {
    setErrors([]);
    setMessage("");
    setForm({
      id: a.id, locationId: a.locationId, name: a.name, leadHours: String(a.leadTimeMinutes / 60),
      from: a.availableFrom, until: a.availableUntil, active: a.active,
    });
  }

  async function save(event: FormEvent) {
    event.preventDefault();
    setErrors([]);
    setMessage("");
    const body = {
      locationId: form.locationId || null,
      name: form.name,
      leadTimeMinutes: Math.round(Number(form.leadHours) * 60),
      availableFrom: form.from,
      availableUntil: form.until,
      active: form.active,
    };
    try {
      if (form.id) {
        await api("PUT", `/api/add-ons/${form.id}`, body);
      } else {
        await api("POST", "/api/add-ons", body);
      }
      setMessage(`Saved ${form.name}.`);
      setForm(empty);
      await load();
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  return (
    <section>
      <h3>Add-ons</h3>
      <p className="muted">Ordered with a booking, not booked on their own. Each needs notice and is only available at certain hours.</p>
      <table>
        <thead><tr><th>Name</th><th>Location</th><th>Notice</th><th>Available</th><th>Status</th><th></th></tr></thead>
        <tbody>
          {addOns.map((a) => (
            <tr key={a.id}>
              <td>{a.name}</td>
              <td>{a.locationName}</td>
              <td>{a.leadTimeMinutes / 60} hours</td>
              <td>{a.availableFrom} to {a.availableUntil}</td>
              <td>{a.active ? "Offered" : "Not offered"}</td>
              <td><button onClick={() => edit(a)}>Edit</button></td>
            </tr>
          ))}
        </tbody>
      </table>

      <form onSubmit={save} className="card">
        <h3 style={{ marginTop: 0 }}>{form.id ? `Edit ${form.name}` : "Add an add-on"}</h3>
        <div className="row">
          <label>Name <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required /></label>
          <label>Location
            <select value={form.locationId} onChange={(e) => setForm({ ...form, locationId: e.target.value })} required>
              <option value="">Choose a location</option>
              {locations.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
            </select>
          </label>
          <label>Notice (hours)
            <input type="number" min={0} step={0.25} value={form.leadHours} onChange={(e) => setForm({ ...form, leadHours: e.target.value })} style={{ width: "6rem" }} />
          </label>
          <label>From <input type="time" value={form.from} onChange={(e) => setForm({ ...form, from: e.target.value })} /></label>
          <label>Until <input type="time" value={form.until} onChange={(e) => setForm({ ...form, until: e.target.value })} /></label>
          <label><input type="checkbox" checked={form.active} onChange={(e) => setForm({ ...form, active: e.target.checked })} /> Offered</label>
          <button type="submit">{form.id ? "Save changes" : "Add"}</button>
          {form.id && <button type="button" onClick={() => setForm(empty)}>Cancel</button>}
        </div>
      </form>
      <Messages errors={errors} success={message} />
    </section>
  );
}
