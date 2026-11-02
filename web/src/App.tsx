import { useEffect, useState } from "react";

type Health = { api: string; database: string };

export function App() {
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

  return (
    <main style={{ fontFamily: "sans-serif", margin: "2rem" }}>
      <h1>Resource booking</h1>
      <p>{message}</p>
    </main>
  );
}
