import { lazy, Suspense, useState } from "react";
import { useTranslation } from "react-i18next";
import { useAuth } from "../context/AuthContext";
import { useResource } from "../utils/useResource";
import DataTable from "../components/DataTable";
import {
  Modal,
  ErrorBanner,
  JsonView,
  ActivityChart,
  CopyButton,
  Badge,
} from "../components/Common";
import ArchiveDialog from "../components/ArchiveDialog";
import OperationDialog from "../components/OperationDialog";
const TerminalDialog = lazy(() => import("../components/TerminalDialog"));
const configuration = {
  containers: {
    columns: ["name", "image", "state", "health", "status"],
    actions: [
      "inspect",
      "logs",
      "start",
      "stop",
      "restart",
      "pause",
      "unpause",
      "kill",
      "exec",
      "terminal",
      "copy_in",
      "copy_out",
      "uploadArchive",
      "downloadArchive",
      "update",
      "remove",
    ],
  },
  images: {
    columns: ["repository", "tag", "sizeBytes", "createdUtc", "status"],
    actions: ["pull", "check", "tags", "remove"],
  },
  stacks: {
    columns: ["project", "runningCount", "serviceCount"],
    actions: ["up", "down", "restart", "pull"],
  },
  metrics: {
    columns: [
      "containerId",
      "cpuPercent",
      "memoryUsage",
      "memoryLimit",
      "networkRxBytes",
      "networkTxBytes",
      "blockReadBytes",
      "blockWriteBytes",
    ],
  },
  events: { columns: ["timeUtc", "type", "action", "actor"] },
  history: { columns: ["atUtc", "method", "path", "status", "elapsedMs"] },
};
export default function Resources({ kind, catalog }) {
  const { api } = useAuth();
  const { t } = useTranslation();
  const resource = useResource(kind);
  const config = configuration[kind];
  const [operation, setOperation] = useState(null),
    [initial, setInitial] = useState({}),
    [inspect, setInspect] = useState(null),
    [terminal, setTerminal] = useState(null),
    [archive, setArchive] = useState(null),
    [inspectError, setInspectError] = useState(null);
  function openInspect(row) {
    setInspectError(null);
    if (kind === "history" || kind === "containers")
      api
        .get(`${kind}/${encodeURIComponent(row.id)}`)
        .then(setInspect)
        .catch(setInspectError);
    else setInspect(row);
  }
  function choose(action, row = {}) {
    if (action === "inspect") {
      openInspect(row);
      return;
    }
    if (action === "uploadArchive" || action === "downloadArchive") {
      setArchive({ row, upload: action === "uploadArchive" });
      return;
    }
    if (action === "terminal") {
      setTerminal(row);
      return;
    }
    const op = catalog.find((op) => op.name === `${kind}_${action}`);
    if (op) {
      setInitial({ ...row });
      setOperation(op);
    }
  }
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">{t("localMachine")}</p>
          <h1>{t(kind)}</h1>
          <p className="muted">{t("autoRefresh")}</p>
        </div>
        {kind === "images" && (
          <div className="row">
            <button className="primary" onClick={() => choose("pull")}>
              ＋ {t("pull")}
            </button>
            <button onClick={() => choose("prune")}>{t("prune")}</button>
          </div>
        )}
      </div>
      <ErrorBanner error={inspectError} />
      <ErrorBanner error={resource.error} retry={resource.refresh} />
      {resource.loading && <p role="status">{t("loading")}</p>}
      {kind === "history" && (
        <section className="panel">
          <h2>{t("activity")}</h2>
          <ActivityChart data={resource.data || []} />
        </section>
      )}
      <DataTable
        data={resource.data || []}
        columns={config.columns}
        storageKey={kind}
        onRefresh={resource.refresh}
        onInspect={openInspect}
        actions={
          config.actions
            ? (row) => (
                <select
                  className="row-actions"
                  aria-label={`${t("operations")} ${row.name || row.repository || row.project}`}
                  value=""
                  onChange={(e) => choose(e.target.value, row)}
                >
                  <option value="">{t("operations")}</option>
                  {config.actions.map((action) => (
                    <option key={action} value={action}>
                      {t(action)}
                    </option>
                  ))}
                </select>
              )
            : undefined
        }
      />
      {inspect && (
        <Modal
          title={`${t("inspect")} · ${inspect.name || inspect.repository || inspect.project || inspect.id || t(kind)}`}
          onClose={() => setInspect(null)}
          wide
        >
          <div className="detail-grid">
            {Object.entries(inspect)
              .filter(([, v]) => v !== null && typeof v !== "object")
              .map(([key, value]) => (
                <div key={key}>
                  <span className="muted small">
                    {t(key, { defaultValue: key })}
                  </span>
                  <p>
                    {/state|status/.test(key) ? (
                      <Badge value={value} />
                    ) : (
                      String(value)
                    )}
                  </p>
                </div>
              ))}
          </div>
          {inspect.id && <CopyButton value={inspect.id} />}
          <JsonView value={inspect} />
        </Modal>
      )}
      {operation && (
        <OperationDialog
          operation={operation}
          initial={initial}
          onClose={() => setOperation(null)}
          onDone={resource.refresh}
        />
      )}{" "}
      {archive && (
        <ArchiveDialog {...archive} onClose={() => setArchive(null)} />
      )}{" "}
      {terminal && (
        <Suspense fallback={<p>{t("loading")}</p>}>
          <TerminalDialog row={terminal} onClose={() => setTerminal(null)} />
        </Suspense>
      )}
    </>
  );
}
