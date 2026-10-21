import { expect, it } from "vitest";
import i18n from "./index";
import { normalizeLocale } from "./localeRegistry";
import { bytes, number } from "./formatters";
it("normalizes aliases and persists direction and locale changes", async () => {
  expect(normalizeLocale("de-DE")).toBe("de");
  expect(normalizeLocale("unknown")).toBe("en");
  await i18n.changeLanguage("ar");
  expect(document.documentElement.dir).toBe("rtl");
  expect(localStorage.getItem("docmon.locale")).toBe("ar");
  await i18n.changeLanguage("ja");
  expect(document.documentElement.lang).toBe("ja");
  expect(document.documentElement.dir).toBe("ltr");
  await i18n.changeLanguage("en-XA");
  expect(i18n.t("containers")).toContain("[");
  await i18n.changeLanguage("en");
});
it("formats values using the selected locale", () => {
  expect(number(1234.5, "de")).toBe("1.234,5");
  expect(bytes(1024, "en")).toBe("1 KiB");
});
