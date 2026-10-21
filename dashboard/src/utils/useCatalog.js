import { useEffect, useState } from "react";
import { useAuth } from "../context/AuthContext";
export function flattenOpenApi(spec) {
  return Object.entries(spec.paths || {}).flatMap(([path, methods]) =>
    Object.entries(methods)
      .filter(([method]) =>
        ["get", "post", "delete", "put", "patch"].includes(method),
      )
      .map(([method, op]) => ({
        binary: !!op.requestBody?.content?.["application/x-tar"],
        name:
          op.operationId ||
          (path === "/health" ? "health" : `${method}_${path}`),
        method: method.toUpperCase(),
        path,
        summary: op.summary,
        parameters: op.parameters || [],
        schema: op.requestBody?.content?.["application/json"]?.schema || {},
        required: [
          ...new Set([
            ...(op.parameters || [])
              .filter((p) => p.required)
              .map((p) => p.name),
            ...(op.requestBody?.content?.["application/json"]?.schema
              ?.required || []),
          ]),
        ],
      })),
  );
}
export function useCatalog() {
  const { api } = useAuth();
  const [operations, setOperations] = useState([]),
    [error, setError] = useState(null);
  useEffect(() => {
    const abort = new AbortController();
    api
      .request("/openapi.json", { signal: abort.signal })
      .then((spec) => setOperations(flattenOpenApi(spec)))
      .catch((error) => {
        if (error.name !== "AbortError") setError(error);
      });
    return () => abort.abort();
  }, [api]);
  return { operations, error };
}
