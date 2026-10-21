import { afterEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import "../i18n";
import DataTable from "./DataTable";
afterEach(cleanup);
it("filters, sorts, paginates, and refreshes a dense resource list", () => {
  const refresh = vi.fn();
  render(
    <DataTable
      data={Array.from({ length: 25 }, (_, i) => ({
        name: `workload-${String(i).padStart(2, "0")}`,
        state: "Running",
      }))}
      columns={["name", "state"]}
      onRefresh={refresh}
    />,
  );
  expect(screen.getByText("workload-00")).toBeVisible();
  expect(screen.queryByText("workload-24")).toBeNull();
  fireEvent.click(screen.getByLabelText("Next"));
  expect(screen.getByText("workload-24")).toBeVisible();
  fireEvent.change(screen.getByLabelText("Search records"), {
    target: { value: "workload-01" },
  });
  expect(screen.getByText("workload-01")).toBeVisible();
  expect(screen.queryByText("workload-24")).toBeNull();
  fireEvent.click(screen.getByText("Refresh"));
  expect(refresh).toHaveBeenCalledOnce();
});
