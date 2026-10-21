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
    .getByLabel("Endpoint", { exact: true })
    .selectOption("system_info");
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
        await expect(page.locator("main > [role=status]")).toHaveCount(0);
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
            .getByRole("button", { name: /Operations/ })
            .click();
          const menu = page.getByRole("menu");
          await expect(menu).toBeVisible();
          const box = await menu.boundingBox();
          expect(box.x).toBeGreaterThanOrEqual(0);
          expect(box.y).toBeGreaterThanOrEqual(0);
          expect(box.x + box.width).toBeLessThanOrEqual(width);
          expect(box.y + box.height).toBeLessThanOrEqual(1000);
          await page
            .getByRole("menuitem", { name: "Restart", exact: true })
            .click();
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
  for (const locale of [
    "es",
    "pt",
    "fr",
    "it",
    "de",
    "zh",
    "ar",
    "ru",
    "ms",
    "hi",
    "ja",
    "en-XA",
    "ar-XB",
    "en",
  ]) {
    await page
      .getByLabel("Language", { exact: true })
      .first()
      .selectOption(locale);
    await expect(page.locator("html")).toHaveAttribute("lang", locale);
    await expect(page.locator("html")).toHaveAttribute(
      "dir",
      locale.startsWith("ar") ? "rtl" : "ltr",
    );
    await page.reload();
    await expect(page.locator("html")).toHaveAttribute("lang", locale);
    // Use a stable class because the localized accessible label changes.
    if (locale !== "en")
      await page.locator(".topbar .locale").selectOption("en");
  }
});
test("charts, request inspector, image identities, and topbar links", async ({
  page,
}) => {
  await login(page);
  await expect(page.locator(".topbar a[aria-label=GitHub]")).toHaveAttribute(
    "href",
    "https://github.com/jchristn/Docmon",
  );
  await expect(page.locator(".topbar a[aria-label=Discord]")).toHaveAttribute(
    "href",
    "https://discord.gg/tRAN8HgvK5",
  );
  await expect(page.getByText("Getting started", { exact: true })).toHaveCount(
    0,
  );
  await expect(page.getByText("Full control", { exact: true })).toHaveCount(0);
  await expect(page.locator(".chart svg")).toContainText("Requests per minute");
  await expect(page.locator(".chart svg")).toContainText("Time");
  await page.goto("/metrics");
  await expect(page.getByRole("tab")).toHaveCount(6);
  await expect(page.getByRole("tabpanel")).toHaveCount(1);
  for (const name of [
    "CPU (%)",
    "Memory used",
    "Network in",
    "Network out",
    "Disk read",
    "Disk write",
  ]) {
    await page.getByRole("tab", { name, exact: true }).click();
    await expect(page.getByRole("tab", { name, exact: true })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    await expect(page.getByRole("tabpanel").locator("svg")).toContainText(
      "Time",
    );
    await expect(page.getByRole("tabpanel").locator("svg")).toContainText(name);
  }
  const target = page.getByLabel("Target", { exact: true });
  if ((await target.locator("option").count()) > 1) {
    await target.selectOption({ index: 1 });
    await expect(
      page.getByText("Container metrics from Docker.", { exact: false }),
    ).toBeVisible();
    await target.selectOption("");
  }
  await page.goto("/history");
  await expect(page.locator("tbody tr").first()).toBeVisible();
  await page.locator("tbody tr").first().click();
  await expect(page.getByRole("dialog")).toContainText("Request details");
  await expect(
    page.getByRole("heading", { name: "Request headers", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Response body", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Close", exact: true }).click();
  await page.goto("/images");
  await expect(
    page.getByRole("columnheader", { name: /ID/ }).first(),
  ).toBeVisible();
  await page
    .getByRole("columnheader", { name: /Repository/ })
    .getByRole("button")
    .click();
  await expect(
    page.getByRole("columnheader", { name: /Repository/ }),
  ).toHaveAttribute("aria-sort", "descending");
});
test("pre-auth language choice persists through login and logout", async ({
  page,
}) => {
  await page.goto("/login");
  await page.getByLabel("Language", { exact: true }).selectOption("es");
  await expect(
    page.getByRole("button", { name: "Conectar a la máquina" }),
  ).toBeVisible();
  await page.reload();
  await expect(page.locator("html")).toHaveAttribute("lang", "es");
  await page.locator(".locale").selectOption("ar");
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
  await page.getByLabel("رمز الوصول", { exact: true }).fill(token);
  await page.getByRole("button", { name: "الاتصال بالجهاز" }).click();
  await expect(
    page.getByRole("heading", { name: "الموارد في لمحة" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "تسجيل الخروج" }).click();
  await expect(page.locator("html")).toHaveAttribute("lang", "ar");
  await expect(
    page.getByRole("button", { name: "الاتصال بالجهاز" }),
  ).toBeVisible();
});
test("localized charts, menus, and request inspector survive expansion and RTL", async ({
  page,
}) => {
  test.setTimeout(60000);
  await login(page);
  for (const locale of ["es", "zh", "ar", "en-XA", "ar-XB"]) {
    await page.locator(".topbar .locale").selectOption(locale);
    for (const route of ["metrics", "history", "images"]) {
      await page.goto(`/${route}`);
      await expect(page.locator("h1").first()).toBeVisible();
      expect(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
      ).toBeTruthy();
      if (route === "history") {
        await expect(page.locator("tbody tr").first()).toBeVisible();
        await page.locator("tbody tr").first().click();
        await expect(page.getByRole("dialog")).toBeVisible();
        await page.keyboard.press("Escape");
      }
      if (route === "images") {
        await expect(page.locator("main > [role=status]")).toHaveCount(0, {
          timeout: 15000,
        });
        await expect(page.locator("tbody tr").first()).toBeVisible();
        await page
          .locator("tbody tr")
          .last()
          .locator("td")
          .last()
          .getByRole("button")
          .click();
        const menu = page.getByRole("menu");
        await expect(menu).toBeVisible();
        const box = await menu.boundingBox();
        const viewport = page.viewportSize();
        expect(box.x).toBeGreaterThanOrEqual(0);
        expect(box.y).toBeGreaterThanOrEqual(0);
        expect(box.x + box.width).toBeLessThanOrEqual(viewport.width);
        expect(box.y + box.height).toBeLessThanOrEqual(viewport.height);
        expect(
          await menu.evaluate((node) => {
            const r = node.getBoundingClientRect();
            return node.contains(
              document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2),
            );
          }),
        ).toBeTruthy();
        await page.keyboard.press("Escape");
      }
      await page.screenshot({
        path: `test-results/${route}-${locale}.png`,
        fullPage: true,
      });
    }
  }
});
test("history timeframes, compact rows, toolbar order, and viewport-sized inspector", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await login(page);
  const order = await page
    .locator(".topbar-actions > button, .topbar-actions > a")
    .evaluateAll((nodes) =>
      nodes.map((node) => node.getAttribute("aria-label")),
    );
  expect(order).toEqual(["Toggle theme", "GitHub", "Discord", "Sign out"]);
  expect(
    await page
      .getByRole("button", { name: "Sign out", exact: true })
      .innerText(),
  ).toBe("");
  await page.goto("/history");
  for (const [value, count] of [
    ["hour", 60],
    ["day", 96],
    ["week", 84],
    ["month", 90],
  ]) {
    await page.getByLabel("Timeframe", { exact: true }).selectOption(value);
    await expect(page.locator(".chart .chart-hover")).toHaveCount(count, {
      timeout: 15000,
    });
  }
  await expect(
    page.getByText("Hover or focus a point to see its time and value.", {
      exact: true,
    }),
  ).toHaveCount(0);
  await page.locator(".chart-hover").first().focus();
  await expect(page.locator(".chart-tooltip")).toBeVisible();
  await expect(page.locator("tbody tr").first()).toBeVisible();
  const padding = await page
    .locator("tbody td")
    .first()
    .evaluate((node) => parseFloat(getComputedStyle(node).paddingTop));
  expect(padding).toBeLessThanOrEqual(4);
  await page.locator("tbody tr").first().click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  const box = await dialog.boundingBox();
  expect(box.width).toBeCloseTo(1440 * 0.95, 0);
  expect(box.height).toBeCloseTo(1000 * 0.95, 0);
});
