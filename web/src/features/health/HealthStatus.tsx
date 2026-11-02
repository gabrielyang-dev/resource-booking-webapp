import { useEffect, useState } from "react";

type Health = { api: string; database: string };

export function HealthStatus() {
  const [message, setMessage] = useState("Checking...");

  useEffect(() => {
    async function checkHealth() {
      try {
        const response = await fetch("/api/health");
        const health: Health = await response.json();
        if (response.ok) {
          setMessage(`API: ${health.api}, database: ${health.database}`);
        } else {
          setMessage(`Unhealthy: API: ${health.api}, database: ${health.database}`);
        }
      } catch {
        setMessage("Unhealthy: the API did not answer.");
      }
    }
    checkHealth();
  }, []);

  return <small style={{ color: "#666" }}>{message}</small>;
}
