import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// In dev, /api calls go to the .NET API (see launchSettings.json for the port).
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      "/api": "http://localhost:5122",
    },
  },
});
