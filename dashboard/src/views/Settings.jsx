import { useTranslation } from "react-i18next";
import { useResource } from "../utils/useResource";
import { LocaleSelector, ErrorBanner, CopyButton } from "../components/Common";
export default function Settings() {
  const { t } = useTranslation();
  const info = useResource("system/info", false);
  return (
    <>
      <div className="page-heading">
        <div>
          <h1>{t("settings")}</h1>
          <p className="muted">{t("settingsHelp")}</p>
        </div>
      </div>
      <ErrorBanner error={info.error} retry={info.refresh} />
      <section className="panel">
        <h2>{t("serverInfo")}</h2>
        <div className="detail-grid">
          {Object.entries(info.data || {})
            .filter(([, value]) => typeof value !== "object" || value === null)
            .map(([key, value]) => (
              <div key={key}>
                <span className="muted">
                  {t(key === "endpoint" ? "endpointLabel" : key, {
                    defaultValue: key,
                  })}
                </span>
                <p>{value || "−"}</p>
                {value && <CopyButton value={String(value)} />}
              </div>
            ))}
        </div>
      </section>
      <section className="panel">
        <h2>{t("token")}</h2>
        <p>{t("tokenHelp")}</p>
        <p className="muted">{t("securityHelp")}</p>
      </section>
      <section className="panel">
        <h2>{t("language")}</h2>
        <LocaleSelector />
      </section>
    </>
  );
}
