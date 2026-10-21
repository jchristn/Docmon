import { useTranslation } from "react-i18next";
import { useResource } from "../utils/useResource";
import TimeChart from "./TimeChart";
import { ErrorBanner } from "./Common";
import { number } from "../i18n/formatters";
export default function RequestActivity({
  timeframe = "hour",
  onTimeframeChange,
}) {
  const { t, i18n } = useTranslation();
  const summary = useResource(`history/summary?timeframe=${timeframe}`);
  return (
    <>
      {onTimeframeChange && (
        <label className="history-timeframe">
          {t("timeframe")}{" "}
          <select
            aria-label={t("timeframe")}
            value={timeframe}
            onChange={(e) => onTimeframeChange(e.target.value)}
          >
            {["hour", "day", "week", "month"].map((value) => (
              <option key={value} value={value}>
                {t(`last${value[0].toUpperCase()}${value.slice(1)}`)}
              </option>
            ))}
          </select>
        </label>
      )}
      <ErrorBanner error={summary.error} retry={summary.refresh} />
      <TimeChart
        stacked
        points={summary.data?.buckets || []}
        label={t(timeframe === "hour" ? "requestCount" : "requestsPerBucket")}
        format={(v) => number(v, i18n.language, { maximumFractionDigits: 0 })}
      />
    </>
  );
}
