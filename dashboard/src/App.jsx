import { useEffect, useState } from "react";
import {
  Navigate,
  NavLink,
  Outlet,
  Route,
  Routes,
  useLocation,
} from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useAuth } from "./context/AuthContext";
import { useTheme } from "./context/ThemeContext";
import { useResource } from "./utils/useResource";
import { useCatalog } from "./utils/useCatalog";
import { ErrorBanner, LocaleSelector } from "./components/Common";
import Overview from "./views/Overview";
import Resources from "./views/Resources";
import Explorer from "./views/Explorer";
import Settings from "./views/Settings";
import Metrics from "./views/Metrics";
const icons = {
  overview: "◫",
  containers: "▣",
  images: "▱",
  stacks: "▤",
  metrics: "⌁",
  events: "↯",
  history: "◷",
  explorer: "⌘",
  settings: "⚙",
};
function Login() {
  const { toggle } = useTheme();
  const { t } = useTranslation();
  const { login, token } = useAuth();
  const [value, setValue] = useState(""),
    [error, setError] = useState(null),
    [busy, setBusy] = useState(false);
  if (token) return <Navigate to="/" replace />;
  return (
    <main className="login">
      <section className="login-story">
        <a href="https://github.com/jchristn/Docmon" className="brand">
          <img src="/assets/logo.png" alt="" />
          <span>Docmon</span>
        </a>
        <div>
          <p className="eyebrow">{t("console")}</p>
          <h1>{t("tagline")}</h1>
          <p>{t("onboardingHelp")}</p>
          <div className="login-tags">
            <span>Docker Engine</span>
            <span>REST API</span>
            <span>MCP</span>
          </div>
        </div>
        <div className="row">
          <a
            href="https://github.com/jchristn/Docmon"
            target="_blank"
            rel="noreferrer"
          >
            {t("github")} ↗
          </a>
          <a
            href="https://discord.gg/tRAN8HgvK5"
            target="_blank"
            rel="noreferrer"
          >
            {t("discord")} ↗
          </a>
        </div>
      </section>
      <section className="login-form">
        <div className="login-tools">
          <button aria-label={t("theme")} onClick={toggle}>
            ◐
          </button>
          <LocaleSelector />
        </div>
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            setBusy(true);
            setError(null);
            try {
              await login(value);
            } catch (err) {
              setError(err);
            } finally {
              setBusy(false);
            }
          }}
        >
          <img className="login-logo" src="/assets/logo.png" alt="Docmon" />
          <p className="eyebrow">{t("localMachine")}</p>
          <h2>{t("connect")}</h2>
          <p className="muted">{t("loginHelp")}</p>
          <label className="field">
            {t("token")}
            <input
              type="password"
              autoComplete="current-password"
              required
              value={value}
              onChange={(e) => setValue(e.target.value)}
              autoFocus
            />
          </label>
          <ErrorBanner error={error} />
          <button className="primary" type="submit" disabled={busy}>
            {t(busy ? "connecting" : "connect")} ↗
          </button>
          <p className="small muted">{t("securityHelp")}</p>
        </form>
      </section>
    </main>
  );
}
function Layout() {
  const { t } = useTranslation();
  const { token, logout } = useAuth();
  const location = useLocation();
  const info = useResource("system/info");
  const { toggle } = useTheme();
  if (!token) return <Navigate to="/login" replace />;
  const current = location.pathname.split("/")[1] || "overview";
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <NavLink to="/" className="brand">
          <img src="/assets/logo.png" alt="" />
          <span>
            Docmon<small>{t("console")}</small>
          </span>
        </NavLink>
        <nav>
          {[
            ["operations", ["overview", "containers", "images", "stacks"]],
            [
              "diagnostics",
              ["metrics", "events", "history", "explorer", "settings"],
            ],
          ].map(([group, pages]) => (
            <div className="nav-group" key={group}>
              <p>{t(group)}</p>
              {pages.map((page) => (
                <NavLink
                  key={page}
                  to={page === "overview" ? "/" : `/${page}`}
                  end
                >
                  <span>{icons[page]}</span>
                  {t(page)}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>
        <footer>
          <small>
            v{info.data?.version || "0.1.1"} · {t("operator")}
          </small>
        </footer>
      </aside>
      <div className="workspace">
        <header className="topbar">
          <strong>{t(current)}</strong>
          <div className="topbar-actions">
            <span
              className={`connection ${info.error ? "offline" : ""}`}
              title={info.data?.endpoint}
            >
              <i />
              {t(info.error ? "disconnected" : "live")}
            </span>
            <LocaleSelector />
            <button aria-label={t("theme")} title={t("theme")} onClick={toggle}>
              ◐
            </button>
            <a
              className="social-link"
              href="https://github.com/jchristn/Docmon"
              target="_blank"
              rel="noreferrer"
              aria-label={t("github")}
              title={t("github")}
            >
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M12 .8a11.3 11.3 0 0 0-3.6 22c.6.1.8-.2.8-.5v-2c-3.3.7-4-1.4-4-1.4-.5-1.4-1.3-1.7-1.3-1.7-1.1-.8.1-.8.1-.8 1.2.1 1.8 1.2 1.8 1.2 1 1.8 2.6 1.3 3.3 1 .1-.8.4-1.3.7-1.6-2.6-.3-5.4-1.3-5.4-5.6 0-1.2.4-2.2 1.2-3-.1-.3-.5-1.5.1-3 0 0 1-.3 3.1 1.2a10.7 10.7 0 0 1 5.7 0C16.8 5.1 17.8 5.4 17.8 5.4c.6 1.5.2 2.7.1 3 .8.8 1.2 1.8 1.2 3 0 4.3-2.8 5.3-5.4 5.6.4.4.8 1.1.8 2.2v3.1c0 .3.2.6.8.5A11.3 11.3 0 0 0 12 .8Z" />
              </svg>
            </a>
            <a
              className="social-link"
              href="https://discord.gg/tRAN8HgvK5"
              target="_blank"
              rel="noreferrer"
              aria-label={t("discord")}
              title={t("discord")}
            >
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M20.3 4.5a20 20 0 0 0-4.9-1.5l-.6 1.2a18 18 0 0 0-5.6 0L8.6 3a20 20 0 0 0-4.9 1.5C.6 9 .1 13.5.4 18a20 20 0 0 0 6 3l1.2-2a12 12 0 0 1-1.9-.9l.5-.4a14.4 14.4 0 0 0 11.6 0l.5.4-1.9.9 1.2 2a20 20 0 0 0 6-3c.4-5.2-.8-9.6-3.3-13.5ZM8.3 15.3c-1.1 0-1.9-1-1.9-2.2s.8-2.2 1.9-2.2 1.9 1 1.9 2.2-.8 2.2-1.9 2.2Zm7.4 0c-1.1 0-1.9-1-1.9-2.2s.8-2.2 1.9-2.2 1.9 1 1.9 2.2-.8 2.2-1.9 2.2Z" />
              </svg>
            </a>
            <button
              className="icon-button"
              onClick={logout}
              aria-label={t("logout")}
              title={t("logout")}
            >
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path
                  d="M10 4H4v16h6M14 8l4 4-4 4M8 12h12"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="1.8"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </svg>
            </button>
          </div>
        </header>
        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
function ConsoleRoutes() {
  const { operations, error } = useCatalog();
  return (
    <>
      <ErrorBanner error={error} />
      <Routes>
        <Route index element={<Overview />} />
        {["containers", "images", "stacks", "events", "history"].map((kind) => (
          <Route
            key={kind}
            path={kind}
            element={<Resources key={kind} kind={kind} catalog={operations} />}
          />
        ))}
        <Route path="metrics" element={<Metrics />} />
        <Route path="explorer" element={<Explorer catalog={operations} />} />
        <Route path="settings" element={<Settings />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </>
  );
}
export default function App() {
  const { t } = useTranslation();
  const location = useLocation();
  const page = location.pathname.split("/")[1] || "overview";
  useEffect(() => {
    document.title = `Docmon · ${t(page === "login" ? "connect" : page)}`;
  }, [page, t]);
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route element={<Layout />}>
        <Route path="/*" element={<ConsoleRoutes />} />
      </Route>
    </Routes>
  );
}
