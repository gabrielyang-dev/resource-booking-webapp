import { useState } from "react";
import { LocationsSection } from "./LocationsSection";
import { ResourceTypesSection } from "./ResourceTypesSection";
import { ResourcesSection } from "./ResourcesSection";
import { RulesSection } from "./RulesSection";
import { AddOnsSection } from "./AddOnsSection";

type Tab = "locations" | "types" | "resources" | "rules" | "addOns";

// Admin only: one section at a time.
export function CataloguePage() {
  const [tab, setTab] = useState<Tab>("resources");
  return (
    <section>
      <h2>Catalogue</h2>
      <p>
        <button disabled={tab === "locations"} onClick={() => setTab("locations")}>Locations</button>{" "}
        <button disabled={tab === "types"} onClick={() => setTab("types")}>Resource types</button>{" "}
        <button disabled={tab === "resources"} onClick={() => setTab("resources")}>Resources</button>{" "}
        <button disabled={tab === "rules"} onClick={() => setTab("rules")}>Rules</button>{" "}
        <button disabled={tab === "addOns"} onClick={() => setTab("addOns")}>Add-ons</button>
      </p>
      {tab === "locations" && <LocationsSection />}
      {tab === "types" && <ResourceTypesSection />}
      {tab === "rules" && <RulesSection />}
      {tab === "addOns" && <AddOnsSection />}
      {tab === "resources" && <ResourcesSection />}
    </section>
  );
}
