import fs from "node:fs";
import path from "node:path";
import { parse } from "@babel/parser";
const source = JSON.parse(fs.readFileSync("src/i18n/locales/en.json", "utf8"));
const approved = new Set([
  "Docmon",
  "Docker Engine",
  "REST API",
  "MCP",
  "OpenAPI",
  "Swagger",
]);
const failures = [];
function visit(node, file) {
  if (!node || typeof node !== "object") return;
  if (node.type === "JSXText") {
    const text = node.value
      .trim()
      .replace(/\s+/g, " ")
      .replace(/[↗·]/g, "")
      .trim();
    if (/[a-zA-Z]{2}/.test(text) && !approved.has(text))
      failures.push(
        `${file}:${node.loc.start.line}: untranslated text ${text}`,
      );
  }
  if (
    node.type === "JSXAttribute" &&
    ["aria-label", "title", "placeholder", "alt"].includes(node.name.name) &&
    node.value?.type === "StringLiteral" &&
    /[a-zA-Z]{2}/.test(node.value.value) &&
    !approved.has(node.value.value)
  )
    failures.push(
      `${file}:${node.loc.start.line}: untranslated accessibility text`,
    );
  if (
    node.type === "CallExpression" &&
    node.callee.name === "t" &&
    node.arguments[0]?.type === "StringLiteral" &&
    !(node.arguments[0].value in source)
  )
    failures.push(
      `${file}:${node.loc.start.line}: unknown translation key ${node.arguments[0].value}`,
    );
  for (const [key, value] of Object.entries(node)) {
    if (["loc", "start", "end"].includes(key)) continue;
    if (Array.isArray(value)) value.forEach((child) => visit(child, file));
    else if (value && typeof value === "object") visit(value, file);
  }
}
function walk(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const file = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(file);
    else if (
      /\.(jsx|js)$/.test(file) &&
      !file.includes(".test.") &&
      !file.includes("/i18n/")
    )
      visit(
        parse(fs.readFileSync(file, "utf8"), {
          sourceType: "module",
          plugins: ["jsx"],
        }),
        file,
      );
  }
}
walk("src");
if (failures.length) {
  console.error(failures.join("\n"));
  process.exit(1);
}
console.log("i18n UI source checks passed");
