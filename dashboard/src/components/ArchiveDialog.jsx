import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useAuth } from "../context/AuthContext";
import { Modal, ErrorBanner } from "./Common";
export default function ArchiveDialog({ row, upload, onClose }) {
  const { api } = useAuth(),
    { t } = useTranslation();
  const [path, setPath] = useState("/tmp"),
    [file, setFile] = useState(null),
    [busy, setBusy] = useState(false),
    [error, setError] = useState(null),
    [done, setDone] = useState(false);
  return (
    <Modal
      title={t(upload ? "uploadArchive" : "downloadArchive")}
      onClose={onClose}
    >
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError(null);
          try {
            const response = await api.raw(
              `/api/v1/containers/${encodeURIComponent(row.id)}/archive?path=${encodeURIComponent(path)}`,
              {
                method: upload ? "PUT" : "GET",
                contentType: upload ? "application/x-tar" : undefined,
                rawBody: upload ? file : undefined,
              },
            );
            if (!response.ok) {
              const error = await response.json();
              throw new Error(error.message || `HTTP ${response.status}`);
            }
            if (!upload) {
              const url = URL.createObjectURL(await response.blob());
              const link = document.createElement("a");
              link.href = url;
              link.download = "container-files.tar";
              link.click();
              setTimeout(() => URL.revokeObjectURL(url), 1000);
            }
            setDone(true);
          } catch (error) {
            setError(error);
          } finally {
            setBusy(false);
          }
        }}
      >
        <p className="notice">{t(upload ? "confirmHelp" : "archiveHelp")}</p>
        <label className="field">
          {t("containerPath")}
          <input
            required
            value={path}
            onChange={(e) => setPath(e.target.value)}
          />
        </label>
        {upload && (
          <label className="field">
            {t("tarFile")}
            <input
              type="file"
              accept=".tar,application/x-tar"
              required
              onChange={(e) => setFile(e.target.files[0])}
            />
          </label>
        )}
        <ErrorBanner error={error} />
        {done && <p role="status">{t("completed")}</p>}
        <div className="modal-actions">
          <button type="button" onClick={onClose}>
            {t("cancel")}
          </button>
          <button
            className={upload ? "danger" : "primary"}
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
