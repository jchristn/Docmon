import { afterEach, expect, it, vi } from "vitest";
import { copyText } from "./clipboard";
afterEach(() => { vi.unstubAllGlobals(); delete document.execCommand; document.body.innerHTML = ""; });
it("copies complete HTTPS URLs with the browser clipboard", async () => {
  const writeText = vi.fn().mockResolvedValue();
  vi.stubGlobal("navigator", { clipboard: { writeText } });
  await copyText("https://docker.example.net:9443");
  expect(writeText).toHaveBeenCalledWith("https://docker.example.net:9443");
});
for (const url of ["http://localhost:8088", "http://192.168.1.20:8088", "https://docker.example.net", "http://[::1]:8088"]) {
  it(`supports insecure-origin clipboard fallback for ${url}`, async () => {
    vi.stubGlobal("navigator", {});
    const button = document.createElement("button"); document.body.appendChild(button); button.focus();
    let copied;
    document.execCommand = vi.fn(() => { copied = document.activeElement.value; return true; });
    await copyText(url);
    expect(copied).toBe(url);
    expect(document.execCommand).toHaveBeenCalledWith("copy");
    expect(document.querySelector("textarea")).toBeNull();
    expect(document.activeElement).toBe(button);
  });
}
it("falls back when browser clipboard permissions reject and reports complete failure", async () => {
  vi.stubGlobal("navigator", { clipboard: { writeText: vi.fn().mockRejectedValue(new Error("Denied")) } });
  document.execCommand = vi.fn(() => true);
  await expect(copyText("http://remote.example:8088")).resolves.toBeUndefined();
  document.execCommand = vi.fn(() => false);
  await expect(copyText("http://remote.example:8088")).rejects.toThrow("Clipboard copy failed");
  expect(document.querySelector("textarea")).toBeNull();
});
