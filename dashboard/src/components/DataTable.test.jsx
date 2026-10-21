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
it("toggles ascending and descending order and exposes sort state", () => {
  render(
    <DataTable
      data={[{ name: "beta" }, { name: "alpha" }]}
      columns={["name"]}
    />,
  );
  const heading = screen.getByRole("columnheader", { name: /Name/ });
  expect(heading).toHaveAttribute("aria-sort", "ascending");
  fireEvent.click(screen.getByRole("button", { name: /Name/ }));
  expect(heading).toHaveAttribute("aria-sort", "descending");
  expect(screen.getAllByRole("row")[2]).toHaveTextContent("beta");
  fireEvent.click(screen.getByRole("button", { name: /Name/ }));
  expect(heading).toHaveAttribute("aria-sort", "ascending");
  expect(screen.getAllByRole("row")[2]).toHaveTextContent("alpha");
});
it("keeps untagged image placeholders after named repositories in either direction", () => {
  render(
    <DataTable
      data={[
        { repository: "—", id: "sha256:abc" },
        { repository: "nginx", id: "sha256:def" },
      ]}
      columns={["repository", "id"]}
    />,
  );
  expect(screen.getAllByRole("row")[2]).toHaveTextContent("nginx");
  fireEvent.click(screen.getByRole("button", { name: /Repository/ }));
  expect(screen.getAllByRole("row")[2]).toHaveTextContent("nginx");
  expect(screen.getByText("Untagged image")).toBeVisible();
});
