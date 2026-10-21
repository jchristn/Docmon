export const number = (value, locale = "en", options = {}) =>
  new Intl.NumberFormat(locale === "en-XA" ? "en" : locale, {
    maximumFractionDigits: 1,
    ...options,
  }).format(value || 0);
export const date = (value, locale = "en") =>
  value
    ? new Intl.DateTimeFormat(locale === "en-XA" ? "en" : locale, {
        dateStyle: "short",
        timeStyle: "medium",
      }).format(new Date(value))
    : "−";
export function bytes(value, locale = "en") {
  const v = Number(value) || 0;
  const unit = Math.min(
    4,
    Math.floor(Math.log(Math.max(1, v)) / Math.log(1024)),
  );
  return `${number(v / 1024 ** unit, locale)} ${["B", "KiB", "MiB", "GiB", "TiB"][unit]}`;
}
