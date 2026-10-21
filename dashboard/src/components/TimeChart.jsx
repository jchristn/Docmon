import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { time, number, date, shortDate } from "../i18n/formatters";
export default function TimeChart({
  points = [],
  label,
  format,
  stacked = false,
}) {
  const { t, i18n } = useTranslation();
  const [hover, setHover] = useState(null);
  const element = useRef(null);
  const [width, setWidth] = useState(1000);
  useEffect(() => {
    const observer = new ResizeObserver((entries) =>
      setWidth(Math.max(280, entries[0].contentRect.width)),
    );
    observer.observe(element.current);
    return () => observer.disconnect();
  }, []);
  const observed = Math.max(
    1,
    ...points.filter((p) => p.hasData !== false).map((p) => (stacked ? p.ok + p.failed : p.value)),
  );
  const max = stacked ? Math.max(4, Math.ceil(observed / 4) * 4) : observed;
  const left = 85,
    right = width - 20,
    top = 25,
    bottom = 260;
  const first = points[0]?.atUtc,
    last = points.at(-1)?.endUtc || points.at(-1)?.atUtc;
  const range = Math.max(1, new Date(last) - new Date(first));
  const x = (p, i) =>
    stacked
      ? left + (i * (right - left)) / Math.max(1, points.length)
      : left + ((new Date(p.atUtc) - new Date(first)) / range) * (right - left);
  const y = (v) => bottom - (v / max) * (bottom - top);
  const describe = (p) =>
    `${date(p.atUtc, i18n.language)}${stacked && p.endUtc ? ` – ${time(p.endUtc, i18n.language)}` : ""} · ${stacked ? `${t("successes")}: ${number(p.ok, i18n.language)} · ${t("failures")}: ${number(p.failed, i18n.language)}` : `${label}: ${p.hasData === false ? t("noObservations") : format(p.value)}`}`;
  const longRange = range > 86400000;
  const active = hover === null ? null : points[hover];
  return (
    <div ref={element} className={stacked ? "chart" : "metric-chart"}>
      <svg
        viewBox={`0 0 ${width} 320`}
        direction="ltr"
        role="img"
        aria-label={label}
        preserveAspectRatio="none"
      >
        {[0, 0.25, 0.5, 0.75, 1].map((f) => (
          <g key={f}>
            <line
              x1={left}
              x2={right}
              y1={y(max * f)}
              y2={y(max * f)}
              stroke="var(--border)"
            />
            <text x={left - 10} y={y(max * f) + 4} textAnchor="end">
              {format(max * f)}
            </text>
          </g>
        ))}
        <text x="18" y="145" textAnchor="middle" transform="rotate(-90 18 145)">
          {label}
        </text>
        <text x={(left + right) / 2} y="312" textAnchor="middle">
          {t("timeAxis")}
        </text>
        {(width < 500 ? [0, 0.5, 1] : [0, 0.25, 0.5, 0.75, 1]).map((f) => (
          <text
            key={f}
            x={left + f * (right - left)}
            y="284"
            textAnchor={f === 0 ? "start" : f === 1 ? "end" : "middle"}
          >
            {first
              ? longRange
                ? shortDate(
                    new Date(new Date(first).getTime() + range * f),
                    i18n.language,
                  )
                : time(
                    new Date(new Date(first).getTime() + range * f),
                    i18n.language,
                    width < 500 ? { second: undefined } : {},
                  )
              : "−"}
          </text>
        ))}
        {!stacked && points.map((p, i) => {
          if (i === 0 || p.hasData === false || points[i - 1].hasData === false) return null;
          return <line key={`segment-${i}`} x1={x(points[i - 1], i - 1)} y1={y(points[i - 1].value)}
            x2={x(p, i)} y2={y(p.value)} stroke="var(--accent)" strokeWidth="2.5" />;
        })}
        {points.map((p, i) => (
          <g
            key={i}
            tabIndex="0"
            className="chart-hover"
            role="graphics-symbol"
            aria-label={describe(p)}
            onMouseEnter={() => setHover(i)}
            onMouseLeave={() => setHover(null)}
            onFocus={() => setHover(i)}
            onBlur={() => setHover(null)}
          >
            <title>{describe(p)}</title>
            {stacked ? (
              <>
                <rect
                  x={x(p, i) + 2}
                  y={y(p.ok)}
                  width={Math.max(1, (right - left) / points.length - 4)}
                  height={bottom - y(p.ok)}
                  fill="var(--accent)"
                />
                <rect
                  x={x(p, i) + 2}
                  y={y(p.ok + p.failed)}
                  width={Math.max(1, (right - left) / points.length - 4)}
                  height={y(p.ok) - y(p.ok + p.failed)}
                  fill="var(--danger)"
                />
                <rect
                  x={x(p, i)}
                  y={top}
                  width={(right - left) / points.length}
                  height={bottom - top}
                  fill="transparent"
                />
              </>
            ) : p.hasData === false ? (
              <rect x={x(p, i)} y={top} width={Math.max(1, (right - left) / points.length)} height={bottom - top} fill="transparent" />
            ) : (
              <>
                <circle
                  cx={x(p, i)}
                  cy={y(p.value)}
                  r="3"
                  fill="var(--accent)"
                />
                <circle
                  cx={x(p, i)}
                  cy={y(p.value)}
                  r="12"
                  fill="transparent"
                />
              </>
            )}
          </g>
        ))}
      </svg>
      {active && (
        <div className="chart-tooltip" role="status">
          {describe(active)}
        </div>
      )}
    </div>
  );
}
