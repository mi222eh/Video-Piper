export interface DownloadProgress {
  status: "starting" | "downloading" | "converting" | "finished" | "error";
  percent?: number;
  message?: string;
  speed?: string;
  eta?: string;
}

export async function browseDirectory(): Promise<string | null> {
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
  try {
    const res = await fetch("/api/health");
    if (!res.ok) return { ytDlp: false, ffmpeg: false };
    return await res.json();
  } catch {
    return { ytDlp: false, ffmpeg: false };
  }
}

export async function readClipboard(): Promise<string> {
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
  return new Promise((resolve, reject) => {
    try {
      const parsed = new URL(url);
      const cleanUrl = new URL(parsed.origin);
      cleanUrl.pathname = parsed.pathname;
      if (parsed.searchParams.has("v")) {
        cleanUrl.searchParams.set("v", parsed.searchParams.get("v")!);
      }

      const query = new URLSearchParams({
        url: cleanUrl.toString(),
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
