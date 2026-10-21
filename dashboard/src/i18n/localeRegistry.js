export const STORAGE_KEY = "docmon.locale";
export const locales = [
  { code: "en", label: "English", dir: "ltr" },
  { code: "de", label: "Deutsch", dir: "ltr" },
  { code: "ja", label: "日本語", dir: "ltr" },
  { code: "ar", label: "العربية", dir: "rtl" },
  { code: "en-XA", label: "Expanded", dir: "ltr" },
];
export const normalizeLocale = (value) =>
  locales.find((l) => l.code.toLowerCase() === value?.toLowerCase())?.code ||
  locales.find((l) => l.code === value?.split("-")[0])?.code ||
  "en";
