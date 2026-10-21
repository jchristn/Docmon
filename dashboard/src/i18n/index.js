import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import LanguageDetector from "i18next-browser-languagedetector";
import resources from "./resources";
import { locales, normalizeLocale, STORAGE_KEY } from "./localeRegistry";
function syncLanguage(value) {
  const locale = normalizeLocale(value);
  document.documentElement.lang = locale;
  document.documentElement.dir = locales.find((l) => l.code === locale).dir;
  localStorage.setItem(STORAGE_KEY, locale);
}
i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    fallbackLng: "en",
    supportedLngs: locales.map((l) => l.code),
    load: "currentOnly",
    interpolation: { escapeValue: false },
    detection: {
      order: ["localStorage", "navigator"],
      lookupLocalStorage: STORAGE_KEY,
      caches: ["localStorage"],
    },
  });
i18n.on("languageChanged", syncLanguage);
syncLanguage(i18n.language);
export default i18n;
