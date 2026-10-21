import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useAuth } from "../context/AuthContext";
import { Modal, ErrorBanner, JsonView } from "./Common";
export default function OperationDialog({
  operation,
  initial = {},
  onClose,
  onDone,
}) {
  const { api } = useAuth();
  const { t } = useTranslation();
  const [values, setValues] = useState({
    ...initial,
    command: '["/bin/sh", "-c", "echo hello"]',
  });
  const [result, setResult] = useState(null),
    [error, setError] = useState(null),
    [busy, setBusy] = useState(false);
  const abort = useRef(null);
  useEffect(() => () => abort.current?.abort(), []);
  const action = operation.name.split("_").slice(1).join("_");
  const changing = operation.method !== "GET";
  const fields = [
    ...new Set([
      ...(operation.required || []),
      ...(action === "pull" && operation.name.startsWith("images")
        ? ["image"]
        : []),
      ...(operation.name.startsWith("stacks_") && changing ? ["service"] : []),
      ...(action === "logs" ? ["tail"] : []),
    ]),
  ].filter((key) => !initial[key]);
  async function run(event) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    abort.current = new AbortController();
    try {
      const params = { ...values };
      if (action === "exec") {
        params.command = JSON.parse(values.command);
        if (
          !Array.isArray(params.command) ||
          !params.command.length ||
          params.command.some((v) => typeof v !== "string")
        )
          throw new Error(t("invalidCommand"));
      } else delete params.command;
      if (params.tail) params.tail = Number(params.tail);
      const value = await api.execute(operation, params, abort.current.signal);
      setResult(value);
      onDone?.();
    } catch (err) {
      if (err.name !== "AbortError") setError(err);
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal
      title={t(action, { defaultValue: operation.summary })}
      onClose={onClose}
      wide={result !== null}
    >
      <form onSubmit={run}>
        {changing && (
          <div className="notice">
            <strong>{t("confirmTitle")}</strong>
            <p>{t("confirmHelp")}</p>
          </div>
        )}
        <div className="target">
          <span>{t("target")}</span>
          <code>
            {initial.name ||
              initial.id ||
              initial.project ||
              values.image ||
              t("localMachine")}
          </code>
        </div>
        {fields.map((field) => (
          <label className="field" key={field}>
            {t({ image: "imageRef", path: "containerPath" }[field] || field)}
            {field === "command" ? (
              <textarea
                value={values[field] || ""}
                onChange={(e) =>
                  setValues((v) => ({ ...v, [field]: e.target.value }))
                }
              />
            ) : (
              <input
                required={(operation.required || []).includes(field)}
                type={field === "tail" ? "number" : "text"}
                min={field === "tail" ? 1 : undefined}
                max={field === "tail" ? 10000 : undefined}
                value={values[field] || ""}
                onChange={(e) =>
                  setValues((v) => ({ ...v, [field]: e.target.value }))
                }
              />
            )}
          </label>
        ))}
        {action === "pull" && operation.name.startsWith("images") && (
          <p className="muted small">{t("imageHint")}</p>
        )}
        {action === "remove" && (
          <label className="checkbox">
            <input
              type="checkbox"
              checked={!!values.force}
              onChange={(e) =>
                setValues((v) => ({ ...v, force: e.target.checked }))
              }
            />
            {t("force")}
          </label>
        )}
        <ErrorBanner error={error} />
        {result !== null && (
          <>
            <div className={`notice ${result.exitCode ? "error" : ""}`}>
              {t(result.exitCode ? "failed" : "completed")}
              {result.exitCode !== undefined &&
                ` · ${t("exitCode")}: ${result.exitCode}`}
            </div>
            <JsonView value={result} />
          </>
        )}
        <div className="modal-actions">
          <button
            type="button"
            onClick={() => {
              abort.current?.abort();
              onClose();
            }}
          >
            {t("cancel")}
          </button>
          <button
            className={changing ? "danger" : "primary"}
            disabled={busy}
            type="submit"
          >
            {t(busy ? "working" : "run")}
          </button>
        </div>
      </form>
    </Modal>
  );
}
