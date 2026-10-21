import { defineConfig } from "@playwright/test";
const external = process.env.DOCMON_TEST_URL;
export default defineConfig({
  testDir: "./e2e",
  timeout: 30000,
  fullyParallel: false,
  workers: 1,
  use: {
    baseURL: external || "http://127.0.0.1:5174",
    headless: true,
    trace: "retain-on-failure",
  },
  webServer: external
    ? undefined
    : [
        {
          command:
            "dotnet run --project ../src/Docmon.Server -f net10.0 --no-build",
          url: "http://127.0.0.1:18082/openapi.json",
          reuseExistingServer: !process.env.CI,
          env: {
            DOCMON_BEARER_TOKEN: "docmon-development-test-token",
            DOCMON_PORT: "18082",
            DOCMON_MCP_PORT: "18083",
          },
        },
        {
          command: "npm run dev -- --port 5174",
          url: "http://127.0.0.1:5174",
          reuseExistingServer: !process.env.CI,
          env: { DOCMON_API_URL: "http://127.0.0.1:18082" },
        },
      ],
});
