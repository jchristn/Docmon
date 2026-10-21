import { useState } from "react";
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
import { ErrorBanner, LocaleSelector, Modal } from "./components/Common";
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
  const [tour, setTour] = useState(false);
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
          <button onClick={() => setTour(true)}>{t("onboarding")} ↗</button>
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
              <small>{t("fullAccess")}</small>
            </span>
            <LocaleSelector />
            <button aria-label={t("theme")} title={t("theme")} onClick={toggle}>
              ◐
            </button>
            <button onClick={logout}>{t("logout")}</button>
          </div>
        </header>
        <main className="content">
          <Outlet />
        </main>
      </div>
      {tour && (
        <Modal title={t("onboarding")} onClose={() => setTour(false)}>
          <p>{t("onboardingHelp")}</p>
          <p className="muted">{t("transportHelp")}</p>
          <button className="primary" onClick={() => setTour(false)}>
            {t("close")}
          </button>
        </Modal>
      )}
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
        <Route path="metrics" element={<Metrics catalog={operations} />} />
        <Route path="explorer" element={<Explorer catalog={operations} />} />
        <Route path="settings" element={<Settings />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </>
  );
}
export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route element={<Layout />}>
        <Route path="/*" element={<ConsoleRoutes />} />
      </Route>
    </Routes>
  );
}
