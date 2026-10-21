import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useResource } from "../utils/useResource";
import { ErrorBanner } from "../components/Common";
import { bytes, number } from "../i18n/formatters";
import Resources from "./Resources";
function LineChart({ points, field, label, format }) {
  const max = Math.max(1, ...points.map((p) => p[field] || 0));
  const values = points
    .map(
      (p, i) =>
        `${20 + (i / Math.max(1, points.length - 1)) * 660},${135 - ((p[field] || 0) / max) * 110}`,
    )
    .join(" ");
  return (
    <section className="panel">
      <div className="row">
        <h2>{label}</h2>
        <code>{format(points.at(-1)?.[field] || 0)}</code>
      </div>
      <svg
        viewBox="0 0 700 160"
        role="img"
        aria-label={label}
        style={{ width: "100%" }}
      >
        <line x1="20" y1="135" x2="680" y2="135" stroke="var(--border)" />
        <polyline
          points={values}
          fill="none"
          stroke="var(--accent)"
          strokeWidth="2"
        />
        <text x="20" y="15" fill="var(--muted)" fontSize="10">
          {format(max)}
        </text>
      </svg>
    </section>
  );
}
export default function Metrics({ catalog }) {
  const { t, i18n } = useTranslation();
  const history = useResource("metrics/history"),
    containers = useResource("containers");
  const [target, setTarget] = useState("");
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
    .map(([, byContainer]) =>
      [...byContainer.values()].reduce(
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
    );
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
          <option value="">{t("localMachine")}</option>
          {(containers.data || []).map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </div>
      <ErrorBanner error={history.error} retry={history.refresh} />
      <div className="overview-grid">
        {[
          "cpuPercent",
          "memoryUsage",
          "networkRxBytes",
          "networkTxBytes",
          "blockReadBytes",
          "blockWriteBytes",
        ].map((field) => (
          <LineChart
            key={field}
            points={points}
            field={field}
            label={t(field)}
            format={(value) =>
              field === "cpuPercent"
                ? `${number(value, i18n.language)}%`
                : bytes(value, i18n.language)
            }
          />
        ))}
      </div>
      <Resources kind="metrics" catalog={catalog} />
    </>
  );
}
