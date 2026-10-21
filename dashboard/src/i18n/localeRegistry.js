export const STORAGE_KEY = "docmon.locale";
export const locales = [
  { code: "en", label: "English", dir: "ltr" },
  { code: "es", label: "Español", dir: "ltr" },
  { code: "pt", label: "Português", dir: "ltr", variant: "pt-BR" },
  { code: "fr", label: "Français", dir: "ltr" },
  { code: "it", label: "Italiano", dir: "ltr" },
  { code: "de", label: "Deutsch", dir: "ltr" },
  { code: "zh", label: "中文", dir: "ltr", variant: "zh-Hans" },
  { code: "ar", label: "العربية", dir: "rtl" },
  { code: "ru", label: "Русский", dir: "ltr" },
  { code: "ms", label: "Bahasa Melayu", dir: "ltr" },
  { code: "hi", label: "हिन्दी", dir: "ltr" },
  { code: "ja", label: "日本語", dir: "ltr" },
  { code: "en-XA", label: "Expanded", dir: "ltr" },
  { code: "ar-XB", label: "RTL test", dir: "rtl" },
];
export const normalizeLocale = (value) =>
  locales.find((l) => l.code.toLowerCase() === value?.toLowerCase())?.code ||
  locales.find((l) => l.code === value?.split("-")[0]?.toLowerCase())?.code ||
  "en";
export const formatLocale = (value) =>
  locales.find((l) => l.code === value)?.variant ||
  (value === "en-XA" || value === "ar-XB" ? "en" : value);
