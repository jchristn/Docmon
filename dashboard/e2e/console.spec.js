import { test, expect } from "@playwright/test";
const token = process.env.DOCMON_TEST_TOKEN || "docmon-development-test-token";
async function login(page) {
  await page.goto("/login");
  await page.getByLabel("Bearer token", { exact: true }).fill(token);
  await page.getByRole("button", { name: "Connect to machine" }).click();
  await expect(
    page.getByRole("heading", { name: "Resources at a glance" }),
  ).toBeVisible();
}
test("rejects a bad token, protects deep links, and clears login on sign out", async ({
  page,
}) => {
  await page.goto("/containers");
  await expect(page).toHaveURL(/login/);
  await page.getByLabel("Bearer token", { exact: true }).fill("wrong-token");
  await page.getByRole("button", { name: "Connect to machine" }).click();
  await expect(page.getByRole("alert")).toBeVisible();
  await page.getByLabel("Bearer token", { exact: true }).fill(token);
  await page.getByRole("button", { name: "Connect to machine" }).click();
  await expect(
    page.getByRole("heading", { name: "Resources at a glance" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/login/);
  expect(
    await page.evaluate(() => sessionStorage.getItem("docmon.token")),
  ).toBeNull();
});
test("navigates every page, inspects records, and runs a read-only OpenAPI operation", async ({
  page,
}) => {
  await login(page);
  for (const path of [
    "containers",
    "images",
    "stacks",
    "metrics",
    "events",
    "history",
    "settings",
    "explorer",
  ]) {
    await page.goto(`/${path}`);
    await expect(page.locator("h1").first()).toBeVisible();
    await expect(page.locator(".error")).toHaveCount(0);
  }
  await page.getByPlaceholder("Search records").fill("system/info");
  await page
    .locator(".endpoint-list button")
    .filter({ hasText: "/api/v1/system/info" })
    .click();
  await page
    .getByRole("button", { name: "Run operation", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: /Response · 200/ }),
  ).toBeVisible();
  await page.goto("/containers");
  await page
    .getByPlaceholder("Search records")
    .fill("impossible-docmon-empty-search");
  await expect(page.getByText("Nothing here yet")).toBeVisible();
  await page.getByPlaceholder("Search records").fill("");
  const rows = page.locator("tbody tr");
  if (await rows.count()) {
    await rows.first().click();
    await expect(page.getByRole("dialog")).toBeVisible();
    await page.getByRole("button", { name: "Close", exact: true }).click();
  }
});
for (const width of [1440, 768, 390])
  for (const theme of ["dark", "light"]) {
    test(`visual walkthrough ${width}px ${theme}`, async ({ page }) => {
      await page.setViewportSize({ width, height: 1000 });
      await page.addInitScript(
        (theme) => localStorage.setItem("docmon.theme", theme),
        theme,
      );
      await page.goto("/login");
      await page.screenshot({
        path: `test-results/login-${width}-${theme}.png`,
        fullPage: true,
      });
      await login(page);
      for (const path of [
        "",
        "containers",
        "images",
        "stacks",
        "metrics",
        "events",
        "history",
        "explorer",
        "settings",
      ]) {
        await page.goto(`/${path}`);
        await expect(page.locator("h1").first()).toBeVisible();
        await expect(page.locator("main [role=status]")).toHaveCount(0);
        await page.waitForTimeout(100);
        expect(
          await page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth + 1,
          ),
        ).toBeTruthy();
        await page.screenshot({
          path: `test-results/${path || "overview"}-${width}-${theme}.png`,
          fullPage: true,
        });
        if (path === "containers" && (await page.locator("tbody tr").count())) {
          await page
            .locator("tbody tr")
            .first()
            .locator("select")
            .selectOption("restart");
          await expect(page.getByRole("dialog")).toBeVisible();
          await expect(
            page.getByText("Confirm machine operation"),
          ).toBeVisible();
          await page.screenshot({
            path: `test-results/confirm-${width}-${theme}.png`,
            fullPage: true,
          });
          await page
            .getByRole("button", { name: "Cancel", exact: true })
            .click();
        }
      }
    });
  }
test("locale persists, including CJK, RTL, and expanded text", async ({
  page,
}) => {
  await login(page);
  for (const locale of ["de", "ja", "ar", "en-XA", "en"]) {
    await page
      .getByLabel("Language", { exact: true })
      .first()
      .selectOption(locale);
    await expect(page.locator("html")).toHaveAttribute("lang", locale);
    await expect(page.locator("html")).toHaveAttribute(
      "dir",
      locale === "ar" ? "rtl" : "ltr",
    );
    await page.reload();
    await expect(page.locator("html")).toHaveAttribute("lang", locale);
    // Use a stable class because the localized accessible label changes.
    if (locale !== "en")
      await page.locator(".topbar .locale").selectOption("en");
  }
});
