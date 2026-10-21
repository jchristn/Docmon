import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import LanguageDetector from "i18next-browser-languagedetector";
import resources, { loaders } from "./resources";
import { locales, normalizeLocale, STORAGE_KEY } from "./localeRegistry";
function syncLanguage(value) {
  const locale = normalizeLocale(value);
  document.documentElement.lang = locale;
  document.documentElement.dir = locales.find((l) => l.code === locale).dir;
  localStorage.setItem(STORAGE_KEY, locale);
  document.title = `Docmon · ${i18n.t("console")}`;
}
const backend = {
  type: "backend",
  read(language, namespace, done) {
    const loader = loaders[`./locales/${normalizeLocale(language)}.json`];
    if (!loader) {
      done(new Error("Unknown locale"));
      return;
    }
    loader()
      .then((module) => done(null, module.default))
      .catch(done);
  },
};
export const ready = i18n
  .use(backend)
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    partialBundledLanguages: true,
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
ready.then(() => syncLanguage(i18n.language));
export default i18n;
