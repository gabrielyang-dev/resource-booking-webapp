import { useState } from "react";
import { LocationsSection } from "./LocationsSection";
import { ResourceTypesSection } from "./ResourceTypesSection";
import { ResourcesSection } from "./ResourcesSection";

type Tab = "locations" | "types" | "resources";

// Admin only: three sections, one at a time.
export function CataloguePage() {
  const [tab, setTab] = useState<Tab>("resources");
  return (
    <section>
      <h2>Catalogue</h2>
      <p>
        <button disabled={tab === "locations"} onClick={() => setTab("locations")}>Locations</button>{" "}
        <button disabled={tab === "types"} onClick={() => setTab("types")}>Resource types</button>{" "}
        <button disabled={tab === "resources"} onClick={() => setTab("resources")}>Resources</button>
      </p>
      {tab === "locations" && <LocationsSection />}
      {tab === "types" && <ResourceTypesSection />}
      {tab === "resources" && <ResourcesSection />}
    </section>
  );
}
