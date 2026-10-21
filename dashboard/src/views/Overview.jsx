import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useResource } from "../utils/useResource";
import { ActivityChart, ErrorBanner, Badge } from "../components/Common";
import { bytes, number } from "../i18n/formatters";
export default function Overview() {
  const { t, i18n } = useTranslation();
  const containers = useResource("containers"),
    images = useResource("images"),
    stacks = useResource("stacks"),
    metrics = useResource("metrics"),
    history = useResource("history");
  const running = (containers.data || []).filter((c) => c.isRunning).length;
  const attention = (containers.data || []).filter(
    (c) => !c.isRunning || c.health === "unhealthy",
  );
  const cards = [
    ["running", running, "/containers"],
    ["stopped", (containers.data?.length || 0) - running, "/containers"],
    ["localImages", images.data?.length || 0, "/images"],
    ["composeProjects", stacks.data?.length || 0, "/stacks"],
  ];
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">{t("console")}</p>
          <h1>{t("resources")}</h1>
          <p className="muted">{t("autoRefresh")}</p>
        </div>
        <Link className="button primary" to="/containers">
          {t("manageContainers")} ↗
        </Link>
      </div>
      {[containers, images, stacks, metrics, history].map((r, i) => (
        <ErrorBanner key={i} error={r.error} retry={r.refresh} />
      ))}
      {[containers, images, stacks, metrics, history].some(
        (r) => r.loading,
      ) && <p role="status">{t("loading")}</p>}
      <div className="kpis">
        {cards.map(([label, value, link]) => (
          <Link to={link} className="kpi" key={label}>
            <span>{t(label)}</span>
            <strong>{number(value, i18n.language)}</strong>
            <small>{t("details")} ↗</small>
          </Link>
        ))}
      </div>
      <div className="overview-grid">
        <section className="panel">
          <div className="row">
            <h2>{t("activity")}</h2>
            <Link to="/history">{t("history")} ↗</Link>
          </div>
          <ActivityChart data={history.data || []} />
        </section>
        <section className="panel">
          <h2>{t("machineState")}</h2>
          <div className="resource-total">
            <span>{t("cpu")}</span>
            <strong>
              {number(
                (metrics.data || []).reduce((sum, m) => sum + m.cpuPercent, 0),
                i18n.language,
              )}
              %
            </strong>
          </div>
          <div className="resource-total">
            <span>{t("memory")}</span>
            <strong>
              {bytes(
                (metrics.data || []).reduce((sum, m) => sum + m.memoryUsage, 0),
                i18n.language,
              )}
            </strong>
          </div>
          <div className="resource-total">
            <span>{t("imageStorage")}</span>
            <strong>
              {bytes(
                (images.data || []).reduce((sum, m) => sum + m.sizeBytes, 0),
                i18n.language,
              )}
            </strong>
          </div>
        </section>
      </div>
      <div className="overview-grid">
        <section className="panel">
          <div className="row">
            <h2>{t("attention")}</h2>
            <Badge
              value={
                attention.length
                  ? `${attention.length} ${t("stopped")}`
                  : t("live")
              }
            />
          </div>
          {attention.length ? (
            attention.slice(0, 8).map((c) => (
              <Link to="/containers" className="attention-item" key={c.id}>
                <span>{c.name}</span>
                <Badge value={c.state} />
              </Link>
            ))
          ) : (
            <p className="muted">{t("healthyMessage")}</p>
          )}
        </section>
        <section className="panel">
          <h2>{t("quickActions")}</h2>
          <div className="quick-links">
            {["containers", "images", "stacks", "explorer"].map((page) => (
              <Link key={page} to={`/${page}`}>
                {t(page)} <span>↗</span>
              </Link>
            ))}
          </div>
        </section>
      </div>
    </>
  );
}
