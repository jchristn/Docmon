import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Badge } from "./Common";
import { formatLocale } from "../i18n/localeRegistry";
import { bytes, date, number } from "../i18n/formatters";
export default function DataTable({
  data = [],
  columns,
  actions,
  onInspect,
  onRefresh,
  storageKey = "table",
}) {
  const { t, i18n } = useTranslation();
  const [filters, setFilters] = useState({});
  const [search, setSearch] = useState(""),
    [sort, setSort] = useState(columns[0]),
    [ascending, setAscending] = useState(!columns[0].endsWith("Utc")),
    [page, setPage] = useState(1);
  const [size, setSize] = useState(
    () => Number(localStorage.getItem(`docmon.${storageKey}.pageSize`)) || 20,
  );
  const [visible, setVisible] = useState(() => {
    try {
      return (
        JSON.parse(localStorage.getItem(`docmon.${storageKey}.columns`)) ||
        columns
      );
    } catch {
      return columns;
    }
  });
  const rows = useMemo(
    () =>
      [...data]
        .filter((row) =>
          JSON.stringify(row).toLowerCase().includes(search.toLowerCase()),
        )
        .filter((row) =>
          Object.entries(filters).every(([key, value]) =>
            String(row[key] ?? "")
              .toLowerCase()
              .includes(value.toLowerCase()),
          ),
        )
        .sort((a, b) => {
          const missing = (value) =>
            value === null ||
            value === undefined ||
            value === "" ||
            value === "—";
          if (missing(a[sort]) !== missing(b[sort]))
            return missing(a[sort]) ? 1 : -1;
          const comparison =
            typeof a[sort] === "number" && typeof b[sort] === "number"
              ? a[sort] - b[sort]
              : String(a[sort] ?? "").localeCompare(
                  String(b[sort] ?? ""),
                  formatLocale(i18n.language),
                );
          return comparison * (ascending ? 1 : -1);
        }),
    [data, search, filters, sort, ascending, i18n.language],
  );
  const pages = Math.max(1, Math.ceil(rows.length / size)),
    current = Math.min(page, pages);
  function render(key, value) {
    if (key === "repository" && (!value || value === "—"))
      return t("untaggedImage");
    if (key === "tag" && (!value || value === "—")) return t("noTag");
    if (value === null || value === undefined || value === "") return "−";
    if (/state|status|health/.test(key)) return <Badge value={value} />;
    if (/Utc$/.test(key)) return date(value, i18n.language);
    if (/Bytes$|memoryUsage|memoryLimit/.test(key))
      return bytes(value, i18n.language);
    if (typeof value === "number") return number(value, i18n.language);
    if (key === "id" || key === "containerId")
      return (
        <code title={value}>{value.replace(/^sha256:/, "").slice(0, 12)}</code>
      );
    return String(value);
  }
  return (
    <section className="table-frame">
      <div className="table-tools">
        <input
          aria-label={t("search")}
          placeholder={t("search")}
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
        />
        <details className="columns">
          <summary>{t("columns")}</summary>
          <div>
            {columns.map((column, index) => (
              <label key={column}>
                <input
                  type="checkbox"
                  checked={visible.includes(column)}
                  disabled={index === 0}
                  onChange={(e) => {
                    const next = e.target.checked
                      ? [...visible, column]
                      : visible.filter((c) => c !== column);
                    setVisible(next);
                    localStorage.setItem(
                      `docmon.${storageKey}.columns`,
                      JSON.stringify(next),
                    );
                  }}
                />
                {t(column, { defaultValue: column })}
              </label>
            ))}
          </div>
        </details>
        <button onClick={onRefresh}>{t("refresh")}</button>
      </div>
      <div className="pagination">
        <span>
          {t("rowsRange", {
            start: rows.length ? (current - 1) * size + 1 : 0,
            end: Math.min(current * size, rows.length),
            total: rows.length,
          })}
        </span>
        <label>
          {t("pageSize")}{" "}
          <select
            value={size}
            onChange={(e) => {
              setSize(Number(e.target.value));
              setPage(1);
              localStorage.setItem(
                `docmon.${storageKey}.pageSize`,
                e.target.value,
              );
            }}
          >
            {[10, 20, 50, 100].map((n) => (
              <option key={n}>{n}</option>
            ))}
          </select>
        </label>
        <div className="page-buttons">
          <button
            disabled={current === 1}
            onClick={() => setPage(1)}
            aria-label={t("first")}
          >
            «
          </button>
          <button
            disabled={current === 1}
            onClick={() => setPage(current - 1)}
            aria-label={t("previous")}
          >
            ‹
          </button>
          <label>
            {t("page")}{" "}
            <input
              aria-label={t("page")}
              type="number"
              min="1"
              max={pages}
              value={current}
              onChange={(e) =>
                setPage(Math.max(1, Math.min(pages, Number(e.target.value))))
              }
            />
          </label>
          <span>/ {pages}</span>
          <button
            disabled={current === pages}
            onClick={() => setPage(current + 1)}
            aria-label={t("next")}
          >
            ›
          </button>
          <button
            disabled={current === pages}
            onClick={() => setPage(pages)}
            aria-label={t("last")}
          >
            »
          </button>
        </div>
      </div>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              {columns
                .filter((c) => visible.includes(c))
                .map((column) => (
                  <th
                    scope="col"
                    key={column}
                    aria-sort={
                      sort === column
                        ? ascending
                          ? "ascending"
                          : "descending"
                        : "none"
                    }
                  >
                    <button
                      onClick={() => {
                        setSort(column);
                        setAscending(sort === column ? !ascending : true);
                      }}
                    >
                      {t(column, { defaultValue: column })}
                      {sort === column ? (ascending ? " ↑" : " ↓") : ""}
                    </button>
                  </th>
                ))}
              {actions && <th scope="col">{t("operations")}</th>}
            </tr>
            <tr className="column-filters">
              {columns
                .filter((c) => visible.includes(c))
                .map((column) => (
                  <th key={column}>
                    <input
                      aria-label={`${t("search")} ${t(column, { defaultValue: column })}`}
                      placeholder={t(column, { defaultValue: column })}
                      value={filters[column] || ""}
                      onChange={(e) => {
                        setFilters((f) => ({ ...f, [column]: e.target.value }));
                        setPage(1);
                      }}
                    />
                  </th>
                ))}
              {actions && <th />}
            </tr>
          </thead>
          <tbody>
            {rows.slice((current - 1) * size, current * size).map((row, i) => (
              <tr
                key={row.id || row.containerId || row.project || i}
                onClick={(e) => {
                  if (!e.target.closest("button,a,input,select,summary"))
                    onInspect?.(row);
                }}
                tabIndex={onInspect ? 0 : undefined}
                onKeyDown={(e) => {
                  if (
                    e.target === e.currentTarget &&
                    (e.key === "Enter" || e.key === " ")
                  ) {
                    e.preventDefault();
                    onInspect?.(row);
                  }
                }}
                className={onInspect ? "clickable" : ""}
              >
                {columns
                  .filter((c) => visible.includes(c))
                  .map((column) => (
                    <td key={column} title={String(row[column] ?? "")}>
                      {render(column, row[column])}
                    </td>
                  ))}
                {actions && (
                  <td onClick={(e) => e.stopPropagation()}>{actions(row)}</td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
        {rows.length === 0 && (
          <div className="empty">
            <strong>{t("empty")}</strong>
            <p>{t("emptyHelp")}</p>
          </div>
        )}
      </div>
    </section>
  );
}
