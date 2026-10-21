import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiClient } from "./api";
afterEach(() => vi.unstubAllGlobals());
describe("API authentication and routing", () => {
  it("sends credentials only to same-origin paths", async () => {
    const fetch = vi.fn().mockResolvedValue(new Response("{}"));
    vi.stubGlobal("fetch", fetch);
    const api = new ApiClient("test-token");
    await api.get("containers");
    expect(fetch.mock.calls[0][1].headers.Authorization).toBe(
      "Bearer test-token",
    );
    await expect(api.raw("//other.example/path")).rejects.toThrow();
    expect(fetch).toHaveBeenCalledTimes(1);
  });
  it("clears the session on 401 and preserves correlation IDs", async () => {
    const logout = vi.fn();
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          new Response(
            JSON.stringify({ message: "Denied", requestId: "req123" }),
            { status: 401 },
          ),
        ),
    );
    await expect(
      new ApiClient("bad", logout).get("containers"),
    ).rejects.toMatchObject({ status: 401, requestId: "req123" });
    expect(logout).toHaveBeenCalledOnce();
  });
  it("encodes resource IDs and preserves typed mutation fields", async () => {
    const fetch = vi.fn().mockResolvedValue(new Response("{}"));
    vi.stubGlobal("fetch", fetch);
    await new ApiClient("token").execute(
      { method: "POST", path: "/api/v1/images/{id}/remove" },
      { id: "sha256:abc/x", force: false },
    );
    expect(fetch.mock.calls[0][0]).toBe(
      "/api/v1/images/sha256%3Aabc%2Fx/remove",
    );
    expect(JSON.parse(fetch.mock.calls[0][1].body).force).toBe(false);
  });
});
it("follows enumeration tokens without truncating collections", async () => {
  const fetch = vi
    .fn()
    .mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          objects: [{ id: 1 }],
          endOfResults: false,
          continuationToken: "cursor+encoded=",
        }),
      ),
    )
    .mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          objects: [{ id: 2 }],
          endOfResults: true,
          continuationToken: null,
        }),
      ),
    );
  vi.stubGlobal("fetch", fetch);
  const rows = await new ApiClient("token").enumerate("images");
  expect(rows).toEqual([{ id: 1 }, { id: 2 }]);
  expect(fetch.mock.calls[1][0]).toContain(
    "continuationToken=cursor%2Bencoded%3D",
  );
  expect(fetch.mock.calls[0][1].headers["Accept-Language"]).toBeTruthy();
});
