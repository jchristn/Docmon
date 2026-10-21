import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
const backend = process.env.DOCMON_API_URL || "http://127.0.0.1:8080";
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: Object.fromEntries(
      ["/api", "/mcp", "/openapi.json", "/swagger"].map((path) => [
        path,
        { target: backend, ws: true },
      ]),
    ),
  },
  test: {
    environment: "jsdom",
    setupFiles: "./src/test-setup.js",
    exclude: ["e2e/**", "node_modules/**"],
  },
});
