import i18n from "../i18n";
export class ApiError extends Error {
  constructor(message, status, requestId, code) {
    super(message);
    this.status = status;
    this.requestId = requestId;
    this.code = code;
  }
}
export class ApiClient {
  constructor(token, onUnauthorized = () => {}) {
    this.token = token;
    this.onUnauthorized = onUnauthorized;
  }
  async raw(path, { method = "GET", body, rawBody, contentType, signal } = {}) {
    if (!path.startsWith("/") || path.startsWith("//"))
      throw new Error("Only same-origin API paths are allowed.");
    const response = await fetch(path, {
      method,
      signal,
      headers: {
        "Accept-Language": i18n.language,
        Authorization: `Bearer ${this.token}`,
        ...(body !== undefined
          ? { "Content-Type": "application/json" }
          : contentType
            ? { "Content-Type": contentType }
            : {}),
      },
      body: body !== undefined ? JSON.stringify(body) : rawBody,
    });
    if (response.status === 401) this.onUnauthorized();
    return response;
  }
  async request(path, options) {
    const response = await this.raw(path, options);
    const result = await response.json();
    if (!response.ok)
      throw new ApiError(
        result.message || `HTTP ${response.status}`,
        response.status,
        result.requestId,
        result.code,
      );
    return result;
  }
  get(resource, signal) {
    return this.request(`/api/v1/${resource}`, { signal });
  }
  async enumerate(resource, signal) {
    const objects = [];
    let continuation;
    do {
      const query = new URLSearchParams({
        maxResults: "1000",
        ...(continuation ? { continuationToken: continuation } : {}),
      });
      const page = await this.get(
        `${resource}${resource.includes("?") ? "&" : "?"}${query}`,
        signal,
      );
      if (!Array.isArray(page.objects))
        throw new ApiError("Invalid enumeration response", 502);
      objects.push(...page.objects);
      if (!page.endOfResults && !page.continuationToken)
        throw new ApiError("Missing continuation token", 502);
      continuation = page.endOfResults ? null : page.continuationToken;
    } while (continuation);
    return objects;
  }
  execute(operation, params = {}, signal) {
    let path = operation.path;
    for (const [key, value] of Object.entries(params))
      path = path.replace(`{${key}}`, encodeURIComponent(value));
    if (/\{.*?\}/.test(path)) throw new Error("Missing path parameter.");
    if (operation.method === "GET" && params.tail)
      path += `?tail=${encodeURIComponent(params.tail)}`;
    if (
      operation.method === "GET" &&
      ["containers_logs", "images_tags"].includes(operation.name)
    )
      return this.enumerate(path.replace(/^\/api\/v1\//, ""), signal);
    return this.request(path, {
      method: operation.method,
      body: operation.method === "GET" ? undefined : params,
      signal,
    });
  }
}
