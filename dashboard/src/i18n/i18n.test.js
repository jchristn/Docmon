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
it("ships complete translated catalogs with matching interpolation tokens", async () => {
  const catalogs = import.meta.glob("./locales/*.json", {
    eager: true,
    import: "default",
  });
  const source = catalogs["./locales/en.json"];
  expect(Object.keys(catalogs)).toHaveLength(12);
  const placeholders = (value) =>
    [...value.matchAll(/\{\{(.*?)\}\}/g)].map((m) => m[1]).sort();
  for (const [path, catalog] of Object.entries(catalogs)) {
    expect(Object.keys(catalog).sort(), path).toEqual(
      Object.keys(source).sort(),
    );
    for (const [key, value] of Object.entries(catalog)) {
      expect(value.trim(), `${path}:${key}`).not.toBe("");
      expect(placeholders(value), `${path}:${key}`).toEqual(
        placeholders(source[key]),
      );
    }
  }
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
    "ar-XB",
  ]) {
    await i18n.changeLanguage(locale);
    expect(i18n.t("loginHelp")).not.toBe(source.loginHelp);
    expect(document.documentElement.dir).toBe(
      locale.startsWith("ar") ? "rtl" : "ltr",
    );
  }
  await i18n.changeLanguage("en");
});
