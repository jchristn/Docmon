import { useCallback, useEffect, useState } from "react";
import { useAuth } from "../context/AuthContext";
export function useResource(path, poll = true) {
  const { api } = useAuth();
  const [data, setData] = useState(null),
    [error, setError] = useState(null),
    [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  const refresh = useCallback(() => setRevision((x) => x + 1), []);
  useEffect(() => {
    setData(null);
    setError(null);
    setLoading(true);
    const controller = new AbortController();
    let active = true,
      timer;
    async function load() {
      try {
        const collection = [
          "containers",
          "images",
          "stacks",
          "metrics",
          "metrics/history",
          "metrics/summary",
          "events",
          "history",
        ].includes(path.split("?")[0]);
        const value = await (collection
          ? api.enumerate(path, controller.signal)
          : api.get(path, controller.signal));
        if (active) {
          setData(value);
          setError(null);
        }
      } catch (err) {
        if (active && err.name !== "AbortError") setError(err);
      } finally {
        if (active) {
          setLoading(false);
          if (poll) timer = setTimeout(load, 5000);
        }
      }
    }
    load();
    return () => {
      active = false;
      controller.abort();
      clearTimeout(timer);
    };
  }, [api, path, poll, revision]);
  return { data, error, loading, refresh };
}
