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
    const controller = new AbortController();
    let active = true,
      timer;
    async function load() {
      try {
        const value = await api.get(path, controller.signal);
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
