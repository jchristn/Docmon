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
        {error.message} {error.requestId && <code>{error.requestId}</code>}
      </span>
      {retry && <button onClick={retry}>{t("retry")}</button>}
    </div>
  );
}
export function Badge({ value }) {
  return (
    <span
      className={`badge ${/running|healthy|200|201|204|start/i.test(String(value)) ? "good" : /exited|unhealthy|dead|[45]\d\d|kill/i.test(String(value)) ? "bad" : ""}`}
    >
      {String(value)}
    </span>
  );
}
export function Modal({ title, children, onClose, wide = false }) {
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
      className={wide ? "wide" : ""}
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
export function ActivityChart({ data = [] }) {
  const { t } = useTranslation();
  const buckets = Array.from({ length: 30 }, () => ({ ok: 0, failed: 0 }));
  const now = Date.now();
  data.forEach((r) => {
    const i = 29 - Math.floor((now - new Date(r.atUtc).getTime()) / 60000);
    if (i >= 0 && i < 30) buckets[i][r.status < 400 ? "ok" : "failed"]++;
  });
  const max = Math.max(1, ...buckets.map((b) => b.ok + b.failed));
  return (
    <div className="chart">
      <svg viewBox="0 0 900 130" role="img" aria-label={t("activity")}>
        {buckets.map((b, i) => (
          <g key={i}>
            <title>{`${b.ok} ${t("successes")}, ${b.failed} ${t("failures")}`}</title>
            <rect
              x={i * 30 + 2}
              y={115 - (b.ok / max) * 100}
              width="22"
              height={Math.max(2, (b.ok / max) * 100)}
              fill="var(--accent)"
              rx="2"
            />
            <rect
              x={i * 30 + 2}
              y={115 - ((b.ok + b.failed) / max) * 100}
              width="22"
              height={(b.failed / max) * 100}
              fill="var(--danger)"
              rx="2"
            />
          </g>
        ))}
      </svg>
      <div className="row muted small">
        <span>−30 min</span>
        <span>
          {t("successes")} / {t("failures")}
        </span>
        <span>0 min</span>
      </div>
    </div>
  );
}
