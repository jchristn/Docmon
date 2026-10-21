export async function copyText(value) {
  if (navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(value);
      return;
    } catch {
      // HTTP origins and browser permissions can require the legacy copy path.
    }
  }
  const previous = document.activeElement;
  const field = document.createElement("textarea");
  field.value = value;
  field.readOnly = true;
  field.style.cssText = "position:fixed;top:0;left:0;opacity:0;pointer-events:none";
  document.body.appendChild(field);
  try {
    field.focus();
    field.select();
    field.setSelectionRange(0, value.length);
    if (!document.execCommand?.("copy")) throw new Error("Clipboard copy failed");
  } finally {
    field.remove();
    previous?.focus?.({ preventScroll: true });
  }
}
