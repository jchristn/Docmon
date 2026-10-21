import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { locales } from "../i18n/localeRegistry";
export function LocaleSelector() {
  const { t, i18n } = useTranslation();
  return (
    <select
      className="locale"
      aria-label={t("language")}
      value={i18n.language}
      onChange={(e) => i18n.changeLanguage(e.target.value)}
    >
      {locales.map((l) => (
        <option key={l.code} value={l.code}>
          {l.label}
        </option>
      ))}
    </select>
  );
}
export function CopyButton({ value }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  return (
    <button
      className="subtle"
      onClick={async () => {
        await navigator.clipboard.writeText(value);
        setCopied(true);
        setTimeout(() => setCopied(false), 1500);
      }}
    >
      {t(copied ? "copied" : "copy")}
    </button>
  );
}
export function ErrorBanner({ error, retry }) {
  const { t } = useTranslation();
  if (!error) return null;
  return (
    <div className="error" role="alert">
      <span>
        {t(error.code || (error.status === 401 ? "authExpired" : "failed"), {
          defaultValue: t("failed"),
        })}{" "}
        <details>
          <summary>{t("details")}</summary>
          <code>{error.message}</code>
        </details>{" "}
        {error.requestId && <code>{error.requestId}</code>}
      </span>
      {retry && <button onClick={retry}>{t("retry")}</button>}
    </div>
  );
}
export function Badge({ value }) {
  const { t } = useTranslation();
  const key = String(value).replace(/^./, (c) => c.toLowerCase());
  const labels = {
    running: "runningCount",
    stopped: "exited",
    unknown: "unknown",
    upToDate: "upToDate",
    updateAvailable: "updateAvailable",
    unsupported: "notSupported",
    current: "upToDate",
    checking: "working",
    error: "failed",
  };
  return (
    <span
      className={`badge ${/running|healthy|200|201|204|start/i.test(String(value)) ? "good" : /exited|unhealthy|dead|[45]\d\d|kill/i.test(String(value)) ? "bad" : ""}`}
    >
      {t(labels[key] || key, { defaultValue: String(value) })}
    </span>
  );
}
export function Modal({
  title,
  children,
  onClose,
  wide = false,
  fullscreen = false,
}) {
  const { t } = useTranslation();
  const dialog = useRef(null);
  useEffect(() => {
    const previous = document.activeElement;
    dialog.current.showModal();
    return () => {
      previous?.focus();
    };
  }, []);
  return (
    <dialog
      ref={dialog}
      className={fullscreen ? "request-details" : wide ? "wide" : ""}
      onCancel={(e) => {
        e.preventDefault();
        onClose();
      }}
      onClick={(e) => {
        if (e.target === dialog.current) onClose();
      }}
    >
      <div className="modal-head">
        <h2>{title}</h2>
        <button onClick={onClose} aria-label={t("close")}>
          ×
        </button>
      </div>
      <div className="modal-body">{children}</div>
    </dialog>
  );
}
export function JsonView({ value }) {
  const { t } = useTranslation();
  const text =
    typeof value === "string" ? value : JSON.stringify(value, null, 2);
  return (
    <section className="json-view">
      <div className="row">
        <strong>{t("details")}</strong>
        <CopyButton value={text || ""} />
      </div>
      <pre>{text}</pre>
    </section>
  );
}
