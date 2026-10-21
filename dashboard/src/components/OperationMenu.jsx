import { useEffect, useRef } from "react";
import { createPortal } from "react-dom";
import { useTranslation } from "react-i18next";

// A native popover enters the browser top layer, above scroll areas and dialogs.
export default function OperationMenu({ actions, label, onChoose }) {
  const { t } = useTranslation();
  const trigger = useRef(null),
    menu = useRef(null);
  useEffect(() => {
    const node = menu.current;
    function position() {
      if (!node.matches(":popover-open")) return;
      const anchor = trigger.current.getBoundingClientRect();
      node.style.maxHeight = `${Math.max(40, window.innerHeight - 16)}px`;
      const box = node.getBoundingClientRect();
      node.style.left = `${Math.max(8, Math.min(anchor.right - box.width, window.innerWidth - box.width - 8))}px`;
      const below = anchor.bottom + 4;
      node.style.top = `${Math.max(8, Math.min(below + box.height <= window.innerHeight - 8 ? below : anchor.top - box.height - 4, window.innerHeight - box.height - 8))}px`;
    }
    node.addEventListener("toggle", position);
    window.addEventListener("resize", position);
    window.addEventListener("scroll", position, true);
    return () => {
      node.removeEventListener("toggle", position);
      window.removeEventListener("resize", position);
      window.removeEventListener("scroll", position, true);
    };
  }, []);
  function open() {
    menu.current.showPopover();
    menu.current.querySelector("button")?.focus();
  }
  return (
    <>
      <button
        ref={trigger}
        aria-label={label}
        aria-haspopup="menu"
        onClick={open}
        onContextMenu={(e) => {
          e.preventDefault();
          open();
        }}
      >
        {t("operations")} ⋮
      </button>
      {createPortal(
        <div
          ref={menu}
          popover="auto"
          role="menu"
          aria-label={label}
          className="operation-menu"
          onKeyDown={(e) => {
            const buttons = [...menu.current.querySelectorAll("button")];
            const index = buttons.indexOf(document.activeElement);
            if (["ArrowDown", "ArrowUp", "Home", "End"].includes(e.key)) {
              e.preventDefault();
              buttons[
                e.key === "Home"
                  ? 0
                  : e.key === "End"
                    ? buttons.length - 1
                    : (index +
                        (e.key === "ArrowDown" ? 1 : -1) +
                        buttons.length) %
                      buttons.length
              ]?.focus();
            }
            if (e.key === "Escape") trigger.current.focus();
          }}
        >
          {actions.map((action) => (
            <button
              key={action}
              role="menuitem"
              onClick={() => {
                menu.current.hidePopover();
                trigger.current.focus();
                onChoose(action);
              }}
            >
              {t(action)}
            </button>
          ))}
        </div>,
        document.body,
      )}
    </>
  );
}
