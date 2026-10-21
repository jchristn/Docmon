import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Terminal } from "@xterm/xterm";
import { FitAddon } from "@xterm/addon-fit";
import "@xterm/xterm/css/xterm.css";
import { useAuth } from "../context/AuthContext";
import { Modal, ErrorBanner, JsonView } from "./Common";
export default function TerminalDialog({ row, onClose }) {
  const { token } = useAuth();
  const { t } = useTranslation();
  const element = useRef(null);
  useEffect(() => {
    const terminal = new Terminal({
      cursorBlink: true,
      fontSize: 14,
      theme: { background: "#151e28" },
    });
    const fit = new FitAddon();
    terminal.loadAddon(fit);
    terminal.open(element.current);
    fit.fit();
    const socket = new WebSocket(
      `${location.protocol === "https:" ? "wss:" : "ws:"}//${location.host}/api/v1/terminal`,
    );
    socket.onopen = () => {
      socket.send(JSON.stringify({ token, id: row.id, command: ["/bin/sh"] }));
      terminal.focus();
    };
    socket.onmessage = (event) => terminal.write(event.data);
    socket.onclose = () => terminal.write(`\r\n[${t("disconnected")}]\r\n`);
    const input = terminal.onData((data) => {
      if (socket.readyState === WebSocket.OPEN) socket.send(data);
    });
    const observer = new ResizeObserver(() => fit.fit());
    observer.observe(element.current);
    return () => {
      observer.disconnect();
      input.dispose();
      socket.close();
      terminal.dispose();
    };
  }, [row.id, token, t]);
  return (
    <Modal title={`${t("terminal")} · ${row.name}`} onClose={onClose} wide>
      <p className="muted">{t("terminalHelp")}</p>
      <div ref={element} className="terminal" />
    </Modal>
  );
}
