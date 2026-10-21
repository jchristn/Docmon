import TimeChart from "../components/TimeChart";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useResource } from "../utils/useResource";
import { ErrorBanner } from "../components/Common";
import { bytes, number } from "../i18n/formatters";
export default function Metrics() {
  const { t, i18n } = useTranslation();
  const history = useResource("metrics/history"),
    containers = useResource("containers");
  const [target, setTarget] = useState("");
  const [field, setField] = useState("cpuPercent");
  const fields = [
    "cpuPercent",
    "memoryUsage",
    "networkRxBytes",
    "networkTxBytes",
    "blockReadBytes",
    "blockWriteBytes",
  ];
  const samples = (history.data || []).filter(
    (s) => !target || s.containerId === target,
  );
  const grouped = new Map();
  samples.forEach((s) => {
    const bucket = Math.floor(new Date(s.atUtc).getTime() / 5000);
    const byContainer = grouped.get(bucket) || new Map();
    const previous = byContainer.get(s.containerId);
    if (!previous || new Date(previous.atUtc) < new Date(s.atUtc))
      byContainer.set(s.containerId, s);
    grouped.set(bucket, byContainer);
  });
  const points = [...grouped.entries()]
    .sort((a, b) => a[0] - b[0])
    .slice(-120)
    .map(([bucket, byContainer]) => ({
      atUtc: new Date(bucket * 5000).toISOString(),
      ...[...byContainer.values()].reduce(
        (row, sample) => {
          for (const key of Object.keys(row)) row[key] += sample[key] || 0;
          return row;
        },
        {
          cpuPercent: 0,
          memoryUsage: 0,
          networkRxBytes: 0,
          networkTxBytes: 0,
          blockReadBytes: 0,
          blockWriteBytes: 0,
        },
      ),
    }));
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">{t("live")}</p>
          <h1>{t("metrics")}</h1>
        </div>
        <select
          aria-label={t("target")}
          value={target}
          onChange={(e) => setTarget(e.target.value)}
        >
          <option value="">{t("dockerWorkloads")}</option>
          {(containers.data || []).map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </div>
      <ErrorBanner error={history.error} retry={history.refresh} />
      <p className="muted small">
        {t(target ? "containerMetricsHint" : "hostMetricsHint")}
      </p>
      <div className="metric-tabs" role="tablist" aria-label={t("metrics")}>
        {fields.map((key) => (
          <button
            key={key}
            id={`tab-${key}`}
            role="tab"
            aria-selected={field === key}
            aria-controls="metric-panel"
            onClick={() => setField(key)}
          >
            {t(key)}
          </button>
        ))}
      </div>
      <section
        className="panel"
        role="tabpanel"
        id="metric-panel"
        aria-labelledby={`tab-${field}`}
      >
        <h2>{t(field)}</h2>
        <TimeChart
          points={points.map((p) => ({ atUtc: p.atUtc, value: p[field] || 0 }))}
          label={t(field)}
          format={(value) =>
            field === "cpuPercent"
              ? `${number(value, i18n.language)}%`
              : bytes(value, i18n.language)
          }
        />
      </section>
    </>
  );
}
