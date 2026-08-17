import React from "react";
import ReactDOM from "react-dom/client";
import * as Neutralino from "@neutralinojs/lib";
import App from "./App";

// Initialize Neutralinojs native client if running in desktop app
try {
  Neutralino.init();
} catch (err) {
  console.info("Neutralino not initialized (running in browser mode):", err);
}

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
