import { useEffect, useState } from "react";
import { api, describeError, Paged, Resource, ResourceType } from "../../api";

type Kind = "OpeningHours" | "MaxDuration" | "BookingWindow" | "DailyQuota";
type Rule = { kind: Kind; settings: Record<string, unknown>; source: string };

const kinds: Kind[] = ["OpeningHours", "MaxDuration", "BookingWindow", "DailyQuota"];
const days = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
const labels: Record<Kind, string> = {
  OpeningHours: "Opening hours",
  MaxDuration: "Maximum duration",
  BookingWindow: "Booking window",
  DailyQuota: "Daily quota per person",
};
const defaults: Record<Kind, Record<string, unknown>> = {
  OpeningHours: { opens: "08:00", closes: "18:00", days: ["Mon", "Tue", "Wed", "Thu", "Fri"] },
  MaxDuration: { maxMinutes: 120 },
  BookingWindow: { minNoticeMinutes: 0, maxDaysAhead: 60 },
  DailyQuota: { maxMinutesPerDay: 480 },
};

// Admin: rules on a resource type (defaults for its resources) or on one resource (overrides).
export function RulesSection() {
  const [types, setTypes] = useState<ResourceType[]>([]);
  const [resources, setResources] = useState<Resource[]>([]);
  // "type:<id>" or "resource:<id>"
  const [target, setTarget] = useState("");
  const [rules, setRules] = useState<Rule[]>([]);
  // The kinds this target sets itself, with their settings being edited.
  const [own, setOwn] = useState<Partial<Record<Kind, Record<string, unknown>>>>({});
  const [errors, setErrors] = useState<string[]>([]);
  const [message, setMessage] = useState("");

  useEffect(() => {
    async function load() {
      try {
        setTypes((await api<Paged<ResourceType>>("GET", "/api/resource-types?pageSize=100")).items);
        setResources((await api<Paged<Resource>>("GET", "/api/resources?pageSize=100&includeInactive=true")).items);
      } catch (error) {
        setErrors(describeError(error));
      }
    }
    load();
  }, []);

  useEffect(() => {
    if (target) loadRules();
  }, [target]);

  const [targetKind, targetId] = target.split(":");
  const path = targetKind === "type" ? `/api/resource-types/${targetId}/rules` : `/api/resources/${targetId}/rules`;

  async function loadRules() {
    setErrors([]);
    setMessage("");
    try {
      const loaded = await api<Rule[]>("GET", path);
      setRules(loaded);
      const mine: Partial<Record<Kind, Record<string, unknown>>> = {};
      for (const rule of loaded) {
        // A type owns all its rules; a resource owns only its overrides.
        if (targetKind === "type" || rule.source === "overridden") mine[rule.kind] = rule.settings;
      }
      setOwn(mine);
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  function toggle(kind: Kind, on: boolean) {
    const inherited = rules.find((r) => r.kind === kind)?.settings;
    const next = { ...own };
    if (on) next[kind] = inherited ?? defaults[kind];
    else delete next[kind];
    setOwn(next);
  }

  function setField(kind: Kind, field: string, value: unknown) {
    setOwn({ ...own, [kind]: { ...own[kind], [field]: value } });
  }

  async function save() {
    setErrors([]);
    setMessage("");
    try {
      const body = kinds.filter((k) => own[k]).map((k) => ({ kind: k, settings: own[k] }));
      await api("PUT", path, body);
      setMessage("Rules saved.");
      await loadRules();
    } catch (error) {
      setErrors(describeError(error));
    }
  }

  function fields(kind: Kind, settings: Record<string, unknown>, editable: boolean) {
    const number = (field: string, label: string) => (
      <label>{label}{" "}
        <input type="number" min={0} disabled={!editable} value={String(settings[field] ?? "")}
          onChange={(e) => setField(kind, field, Number(e.target.value))} style={{ width: "6rem" }} />
      </label>
    );
    if (kind === "OpeningHours") {
      const openDays = (settings.days as string[]) ?? [];
      return (
        <>
          <label>Opens <input type="time" disabled={!editable} value={String(settings.opens)} onChange={(e) => setField(kind, "opens", e.target.value)} /></label>{" "}
          <label>Closes <input type="time" disabled={!editable} value={String(settings.closes)} onChange={(e) => setField(kind, "closes", e.target.value)} /></label>{" "}
          {days.map((d) => (
            <label key={d}>
              <input type="checkbox" disabled={!editable} checked={openDays.includes(d)}
                onChange={(e) => setField(kind, "days", e.target.checked ? days.filter((x) => x === d || openDays.includes(x)) : openDays.filter((x) => x !== d))} />
              {d}{" "}
            </label>
          ))}
        </>
      );
    }
    if (kind === "MaxDuration") return number("maxMinutes", "Minutes");
    if (kind === "BookingWindow") return <>{number("minNoticeMinutes", "Notice (minutes)")} {number("maxDaysAhead", "Days ahead")}</>;
    return number("maxMinutesPerDay", "Minutes per day");
  }

  return (
    <section>
      <h3>Rules</h3>
      <p>
        <label>Rules for{" "}
          <select value={target} onChange={(e) => setTarget(e.target.value)}>
            <option value="">Choose a resource type or resource</option>
            <optgroup label="Resource types (defaults)">
              {types.map((t) => <option key={t.id} value={`type:${t.id}`}>{t.name}</option>)}
            </optgroup>
            <optgroup label="Resources (overrides)">
              {resources.map((r) => <option key={r.id} value={`resource:${r.id}`}>{r.name} ({r.resourceTypeName})</option>)}
            </optgroup>
          </select>
        </label>
      </p>
      {target && (
        <>
          <table cellPadding={6}>
            <tbody>
              {kinds.map((kind) => {
                const effective = rules.find((r) => r.kind === kind);
                const owned = own[kind];
                const source = targetKind === "type" ? (owned ? "set" : "not set")
                  : owned ? "overridden" : effective ? "inherited" : "not set";
                return (
                  <tr key={kind}>
                    <td>
                      <label>
                        <input type="checkbox" checked={!!owned} onChange={(e) => toggle(kind, e.target.checked)} />{" "}
                        {labels[kind]}
                      </label>
                    </td>
                    <td><em>{source}</em></td>
                    <td>{owned ? fields(kind, owned, true) : effective ? fields(kind, effective.settings, false) : null}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <p><button onClick={save}>Save rules</button></p>
          {targetKind === "resource" && <p>Tick a rule to override it for this resource; untick to go back to the type's rule.</p>}
        </>
      )}
      {errors.map((m) => <p key={m} style={{ color: "#b00020" }}>{m}</p>)}
      {message && <p>{message}</p>}
    </section>
  );
}
