import { useTranslation } from "react-i18next";
import { Modal, Badge, CopyButton, JsonView } from "./Common";
import { date, duration } from "../i18n/formatters";
function body(value) {
  try {
    return JSON.parse(value);
  } catch {
    return value || "";
  }
}
export default function RequestDetails({ record, onClose }) {
  const { t, i18n } = useTranslation();
  return (
    <Modal fullscreen title={t("requestDetails")} onClose={onClose}>
      <div className="request-inspector">
        <div className="request-summary">
          <Badge value={record.status} />
          <strong>{record.method}</strong>
          <code>{record.path}</code>
          <span>{date(record.atUtc, i18n.language)}</span>
          <span>{duration(record.elapsedMs, i18n.language)}</span>
        </div>
        <div className="row">
          <code>{record.id}</code>
          <CopyButton value={record.id} />
        </div>
        {record.truncated && <p className="muted">{t("bodyTruncated")}</p>}
        <div className="request-blocks">
          {[
            ["requestHeaders", record.requestHeaders],
            ["responseHeaders", record.responseHeaders],
            ["requestBody", body(record.requestBody)],
            ["responseBody", body(record.responseBody)],
          ].map(([key, value]) => (
            <section key={key}>
              <h3>{t(key)}</h3>
              <JsonView value={value} />
            </section>
          ))}
        </div>
      </div>
    </Modal>
  );
}
