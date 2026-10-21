import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useAuth } from "../context/AuthContext";
import { ErrorBanner, JsonView, Modal, CopyButton } from "../components/Common";
export default function Explorer({ catalog }) {
  const { api } = useAuth();
  const { t } = useTranslation();
  const [search, setSearch] = useState(""),
    [name, setName] = useState(""),
    [params, setParams] = useState({}),
    [body, setBody] = useState("{}");
  const [archiveFile, setArchiveFile] = useState(null);
  const [result, setResult] = useState(null),
    [error, setError] = useState(null),
    [busy, setBusy] = useState(false),
    [confirm, setConfirm] = useState(false);
  const operations = useMemo(
    () =>
      catalog.filter((op) =>
        `${op.method} ${op.path} ${op.summary}`
          .toLowerCase()
          .includes(search.toLowerCase()),
      ),
    [catalog, search],
  );
  const selected = catalog.find((op) => op.name === name) || operations[0];
  let path = selected?.path || "";
  Object.entries(params).forEach(([key, value]) => {
    path = path.replace(`{${key}}`, encodeURIComponent(value));
  });
  const query = new URLSearchParams(
    Object.fromEntries(
      Object.entries(params).filter(
        ([key, value]) =>
          value &&
          selected?.parameters.some((p) => p.name === key && p.in === "query"),
      ),
    ),
  );
  if (query.size) path += `?${query}`;
  async function execute() {
    setConfirm(false);
    setBusy(true);
    setError(null);
    try {
      if (/\{.*?\}/.test(path)) throw new Error(t("apiBodyHint"));
      if (selected.binary && !archiveFile) throw new Error(t("tarFile"));
      const started = performance.now();
      const response = await api.raw(path, {
        method: selected.method,
        body:
          selected.method === "GET" || selected.binary
            ? undefined
            : JSON.parse(body),
        rawBody: selected.binary ? archiveFile : undefined,
        contentType: selected.binary ? "application/x-tar" : undefined,
      });
      let value;
      if (
        response.ok &&
        response.headers.get("Content-Type")?.includes("application/x-tar")
      ) {
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = "container-files.tar";
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
        value = { bytes: blob.size, contentType: blob.type };
      } else {
        const text = await response.text();
        try {
          value = JSON.parse(text);
        } catch {
          value = text;
        }
      }
      setResult({
        status: response.status,
        elapsedMs: Math.round(performance.now() - started),
        headers: Object.fromEntries(response.headers),
        body: value,
      });
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">OpenAPI</p>
          <h1>{t("explorer")}</h1>
          <p className="muted">{t("apiHelp")}</p>
        </div>
        <a href="/swagger" target="_blank" rel="noreferrer">
          Swagger ↗
        </a>
      </div>
      <div className="explorer-grid">
        <section className="panel endpoint-list">
          <input
            aria-label={t("search")}
            placeholder={t("search")}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          {operations.map((op) => (
            <button
              key={op.name}
              className={selected?.name === op.name ? "selected" : ""}
              onClick={() => {
                setName(op.name);
                setParams({});
                setBody("{}");
                setResult(null);
                setArchiveFile(null);
              }}
            >
              <span className={`method ${op.method.toLowerCase()}`}>
                {op.method}
              </span>
              <span>
                {op.summary}
                <code>{op.path}</code>
              </span>
            </button>
          ))}
        </section>
        <section className="panel">
          {selected && (
            <>
              <h2>{selected.summary}</h2>
              <code className="endpoint">
                {selected.method} {path}
              </code>
              {selected.parameters.map((p) => (
                <label className="field" key={p.name}>
                  {p.name}
                  {p.required ? " *" : ""}
                  <input
                    value={params[p.name] || ""}
                    onChange={(e) =>
                      setParams((v) => ({ ...v, [p.name]: e.target.value }))
                    }
                  />
                </label>
              ))}
              {selected.binary && (
                <label className="field">
                  {t("tarFile")}
                  <input
                    type="file"
                    accept=".tar,application/x-tar"
                    onChange={(e) => setArchiveFile(e.target.files[0])}
                  />
                </label>
              )}
              {selected.method !== "GET" && !selected.binary && (
                <label className="field">
                  {t("requestBody")}
                  <textarea
                    className="body-editor"
                    value={body}
                    onChange={(e) => setBody(e.target.value)}
                  />
                  <span className="muted small">{t("apiBodyHint")}</span>
                </label>
              )}
              <div className="row">
                <button
                  className="primary"
                  disabled={busy}
                  onClick={() =>
                    selected.method === "GET" ? execute() : setConfirm(true)
                  }
                >
                  {t(busy ? "working" : "run")}
                </button>
                <CopyButton
                  value={`curl -X ${selected.method} '${location.origin}${path}' -H 'Authorization: Bearer $DOCMON_BEARER_TOKEN'${selected.method !== "GET" ? ` -H 'Content-Type: application/json' --data '${body.replaceAll("'", "'\\''")}'` : ""}`}
                />
              </div>
              <ErrorBanner error={error} />
              {result && (
                <>
                  <h3>
                    {t("response")} · {result.status} · {result.elapsedMs} ms
                  </h3>
                  <JsonView value={result.body} />
                  <details>
                    <summary>{t("responseHeaders")}</summary>
                    <JsonView value={result.headers} />
                  </details>
                </>
              )}
              <details>
                <summary>{t("requestBody")} schema</summary>
                <JsonView value={selected.schema} />
              </details>
            </>
          )}
        </section>
      </div>
      {confirm && (
        <Modal title={t("confirmTitle")} onClose={() => setConfirm(false)}>
          <p>{t("confirmHelp")}</p>
          <code>
            {selected.method} {path}
          </code>
          <div className="modal-actions">
            <button onClick={() => setConfirm(false)}>{t("cancel")}</button>
            <button className="danger" onClick={execute}>
              {t("run")}
            </button>
          </div>
        </Modal>
      )}
    </>
  );
}
