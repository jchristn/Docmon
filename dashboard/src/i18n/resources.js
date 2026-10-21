import en from "./locales/en.json";
export const expanded = Object.fromEntries(
  Object.entries(en).map(([key, value]) => [
    key,
    `[ ${value} ${value.replace(/\{\{.*?\}\}/g, "")} ]`,
  ]),
);
export default {
  en: { translation: en },
  "en-XA": { translation: expanded },
  "ar-XB": {
    translation: Object.fromEntries(
      Object.entries(expanded).map(([key, value]) => [
        key,
        `\u202b${value}\u202c`,
      ]),
    ),
  },
};
export const loaders = import.meta.glob("./locales/*.json");
