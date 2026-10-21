import { afterEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import Metrics from "./Metrics";
import { useResource } from "../utils/useResource";
import en from "../i18n/locales/en.json";
vi.mock("../utils/useResource", () => ({ useResource: vi.fn() }));
vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key) => en[key] || key, i18n: { language: "en" } }),
}));
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
it("requests each historical window for the selected container and keeps gaps distinct", () => {
  vi.stubGlobal("ResizeObserver", class { observe() {} disconnect() {} });
  useResource.mockImplementation((path) => ({
    data: path === "containers" ? [{ id: "abc", name: "web-api" }] : path.startsWith("metrics/summary") ? [
      { atUtc: "2026-10-20T10:00:00Z", cpuPercent: 20, hasData: true },
      { atUtc: "2026-10-20T10:01:00Z", cpuPercent: 0, hasData: false },
      { atUtc: "2026-10-20T10:02:00Z", cpuPercent: 30, hasData: true },
    ] : [], error: null, refresh: vi.fn(),
  }));
  const { container } = render(<Metrics />);
  expect(screen.getByLabelText(en.timeframe)).toHaveValue("live");
  fireEvent.change(screen.getByLabelText(en.target), { target: { value: "abc" } });
  for (const timeframe of ["hour", "day", "week", "month"]) {
    fireEvent.change(screen.getByLabelText(en.timeframe), { target: { value: timeframe } });
    expect(useResource).toHaveBeenCalledWith(`metrics/summary?timeframe=${timeframe}&containerIdFilter=abc`);
    expect(screen.getByText(en.metricHistoryHint)).toBeVisible();
  }
  expect(container.querySelectorAll('circle[r="3"]')).toHaveLength(2);
  expect(container.querySelector('svg line[stroke="var(--accent)"]')).toBeNull();
  expect(screen.getByLabelText(new RegExp(en.noObservations))).toBeInTheDocument();
  fireEvent.change(screen.getByLabelText(en.target), { target: { value: "" } });
  expect(useResource).toHaveBeenCalledWith("metrics/summary?timeframe=month&containerIdFilter=");
  for (const tab of screen.getAllByRole("tab")) fireEvent.click(tab);
  expect(screen.getAllByRole("tab")).toHaveLength(6);
  fireEvent.change(screen.getByLabelText(en.timeframe), { target: { value: "live" } });
  expect(useResource).toHaveBeenCalledWith("metrics/history");
});
