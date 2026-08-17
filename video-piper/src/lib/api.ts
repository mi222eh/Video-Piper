import * as Neutralino from "@neutralinojs/lib";

export interface DownloadProgress {
  status: "starting" | "downloading" | "converting" | "finished" | "error";
  percent?: number;
  message?: string;
  speed?: string;
  eta?: string;
}

function isNeutralinoRunning(): boolean {
  return typeof window !== "undefined" && typeof (window as unknown as { NL_PORT?: number }).NL_PORT !== "undefined";
}

export async function browseDirectory(): Promise<string | null> {
  if (isNeutralinoRunning()) {
    try {
      const selected = await Neutralino.os.showFolderDialog("Välj mapp att spara till");
      return selected || null;
    } catch (err) {
      console.error("Neutralino folder dialog error:", err);
      return null;
    }
  }

  try {
    const res = await fetch("/api/browse", { method: "POST" });
    if (!res.ok) return null;
    const data = await res.json();
    return data.path || null;
  } catch (err) {
    console.error("Failed to browse directory:", err);
    return null;
  }
}

export async function checkSystem(): Promise<{ ytDlp: boolean; ytDlpVersion?: string; ffmpeg: boolean }> {
  if (isNeutralinoRunning()) {
    let ytDlp = false;
    let ytDlpVersion = "";
    let ffmpeg = false;

    try {
      const res = await Neutralino.os.execCommand("yt-dlp --version");
      if (res.exitCode === 0 && res.stdOut) {
        ytDlp = true;
        ytDlpVersion = res.stdOut.trim();
      }
    } catch {
      ytDlp = false;
    }

    try {
      const res = await Neutralino.os.execCommand("ffmpeg -version");
      if (res.exitCode === 0) {
        ffmpeg = true;
      }
    } catch {
      ffmpeg = false;
    }

    return { ytDlp, ytDlpVersion, ffmpeg };
  }

  try {
    const res = await fetch("/api/health");
    if (!res.ok) return { ytDlp: false, ffmpeg: false };
    return await res.json();
  } catch {
    return { ytDlp: false, ffmpeg: false };
  }
}

export async function readClipboard(): Promise<string> {
  if (isNeutralinoRunning()) {
    try {
      return await Neutralino.clipboard.readText();
    } catch (err) {
      console.warn("Neutralino clipboard error:", err);
    }
  }

  try {
    return await navigator.clipboard.readText();
  } catch (err) {
    console.warn("Clipboard read error:", err);
    return "";
  }
}

export async function downloadAudio(
  url: string,
  savePath: string,
  onProgress?: (progress: DownloadProgress) => void
): Promise<boolean> {
  const parsed = new URL(url);
  const cleanUrl = new URL(parsed.origin);
  cleanUrl.pathname = parsed.pathname;
  if (parsed.searchParams.has("v")) {
    cleanUrl.searchParams.set("v", parsed.searchParams.get("v")!);
  }
  const targetUrl = cleanUrl.toString();

  if (isNeutralinoRunning()) {
    return new Promise((resolve, reject) => {
      onProgress?.({ status: "starting", message: "Startar nedladdning..." });

      const pathArg = savePath ? `-P "${savePath}"` : "";
      const command = `yt-dlp -x --audio-format mp3 --newline --progress ${pathArg} "${targetUrl}"`;

      let procId: number | null = null;

      const eventHandler = (evt: CustomEvent) => {
        if (procId === null || evt.detail.id !== procId) return;

        const action = evt.detail.action;
        const data = evt.detail.data;

        if (action === "stdOut" || action === "stdErr") {
          const lines = String(data).split("\n");
          for (const line of lines) {
            const trimmed = line.trim();
            if (!trimmed) continue;

            const match = trimmed.match(
              /\[download\]\s+([\d.]+)%\s+of\s+~?([\d.]+\w+)\s+at\s+([\d.]+\w+\/s)\s+ETA\s+([\d:]+)/
            );
            if (match) {
              onProgress?.({
                status: "downloading",
                percent: parseFloat(match[1]),
                speed: match[3],
                eta: match[4],
                message: trimmed,
              });
            } else if (trimmed.includes("[ExtractAudio]") || trimmed.includes("[ffmpeg]")) {
              onProgress?.({
                status: "converting",
                percent: 99,
                message: "Konverterar till MP3...",
              });
            } else {
              onProgress?.({
                status: "downloading",
                message: trimmed,
              });
            }
          }
        } else if (action === "exit") {
          Neutralino.events.off("spawnedProcess", eventHandler);
          const exitCode = evt.detail.data;
          if (exitCode === 0) {
            onProgress?.({ status: "finished", percent: 100, message: "Klar!" });
            resolve(true);
          } else {
            reject(new Error(`yt-dlp avslutades med felkod ${exitCode}`));
          }
        }
      };

      Neutralino.events.on("spawnedProcess", eventHandler).then(() => {
        Neutralino.os
          .spawnProcess(command)
          .then((proc) => {
            procId = proc.id;
          })
          .catch((err) => {
            Neutralino.events.off("spawnedProcess", eventHandler);
            reject(err);
          });
      });
    });
  }

  // Fallback to HTTP SSE endpoint if not in Neutralino
  return new Promise((resolve, reject) => {
    try {
      const query = new URLSearchParams({
        url: targetUrl,
        savePath: savePath || "",
      });

      const eventSource = new EventSource(`/api/download?${query.toString()}`);

      eventSource.onmessage = (event) => {
        try {
          const data: DownloadProgress = JSON.parse(event.data);
          onProgress?.(data);

          if (data.status === "finished") {
            eventSource.close();
            resolve(true);
          } else if (data.status === "error") {
            eventSource.close();
            reject(new Error(data.message || "Download failed"));
          }
        } catch {
          // ignore non-JSON messages
        }
      };

      eventSource.onerror = (err) => {
        eventSource.close();
        reject(err);
      };
    } catch (err) {
      reject(err);
    }
  });
}
