import { formatLocale } from "./localeRegistry";
export const number = (value, locale = "en", options = {}) =>
  new Intl.NumberFormat(formatLocale(locale), {
    maximumFractionDigits: 1,
    ...options,
  }).format(value || 0);
export const date = (value, locale = "en") =>
  value
    ? new Intl.DateTimeFormat(formatLocale(locale), {
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

export const time = (value, locale = "en", options = {}) =>
  new Intl.DateTimeFormat(formatLocale(locale), {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    ...options,
  }).format(new Date(value));

export const percent = (value, locale) =>
  number(value / 100, locale, { style: "percent" });
export const list = (values, locale) =>
  new Intl.ListFormat(formatLocale(locale), {
    style: "long",
    type: "conjunction",
  }).format(values);
export const relativeTime = (value, unit, locale) =>
  new Intl.RelativeTimeFormat(formatLocale(locale), { numeric: "auto" }).format(
    value,
    unit,
  );

export const duration = (value, locale) =>
  number(value, locale, {
    style: "unit",
    unit: "millisecond",
    unitDisplay: "short",
  });

export const shortDate = (value, locale) =>
  new Intl.DateTimeFormat(formatLocale(locale), {
    month: "short",
    day: "numeric",
  }).format(new Date(value));
