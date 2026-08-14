// Video-Piper Deno Backend & Desktop Host
import { serveDir, serveFile } from "jsr:@std/http/file-server";

const PORT = 1420;

// Configure desktop window if running under `deno desktop`
if ("BrowserWindow" in Deno) {
  // @ts-ignore: Deno.BrowserWindow is available under deno desktop
  new Deno.BrowserWindow({
    title: "Video Piper",
    width: 680,
    height: 560,
    resizable: true,
  });
}

// Cross-platform folder picker using native OS dialog utilities
async function pickFolder(): Promise<string | null> {
  const os = Deno.build.os;
  try {
    if (os === "linux") {
      try {
        const res = await new Deno.Command("zenity", {
          args: ["--file-selection", "--directory", "--title=Välj mapp att spara till"],
          stdout: "piped",
          stderr: "null",
        }).output();
        if (res.code === 0) {
          return new TextDecoder().decode(res.stdout).trim() || null;
        }
      } catch {
        const res = await new Deno.Command("kdialog", {
          args: ["--getexistingdirectory", ""],
          stdout: "piped",
          stderr: "null",
        }).output();
        if (res.code === 0) {
          return new TextDecoder().decode(res.stdout).trim() || null;
        }
      }
    } else if (os === "windows") {
      const psScript = `
        Add-Type -AssemblyName System.Windows.Forms
        $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
        $dialog.Description = "Välj mapp att spara till"
        if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
          Write-Output $dialog.SelectedPath
        }
      `;
      const res = await new Deno.Command("powershell", {
        args: ["-NoProfile", "-NonInteractive", "-Command", psScript],
        stdout: "piped",
        stderr: "null",
      }).output();
      if (res.code === 0) {
        return new TextDecoder().decode(res.stdout).trim() || null;
      }
    } else if (os === "darwin") {
      const res = await new Deno.Command("osascript", {
        args: ["-e", 'POSIX path of (choose folder with prompt "Välj mapp att spara till:")'],
        stdout: "piped",
        stderr: "null",
      }).output();
      if (res.code === 0) {
        return new TextDecoder().decode(res.stdout).trim() || null;
      }
    }
  } catch (err) {
    console.error("Error launching folder picker:", err);
  }
  return null;
}

// Check for yt-dlp and ffmpeg presence
async function checkTools() {
  let ytDlp = false;
  let ytDlpVersion = "";
  let ffmpeg = false;

  try {
    const res = await new Deno.Command("yt-dlp", {
      args: ["--version"],
      stdout: "piped",
      stderr: "null",
    }).output();
    if (res.code === 0) {
      ytDlp = true;
      ytDlpVersion = new TextDecoder().decode(res.stdout).trim();
    }
  } catch {
    ytDlp = false;
  }

  try {
    const res = await new Deno.Command("ffmpeg", {
      args: ["-version"],
      stdout: "piped",
      stderr: "null",
    }).output();
    if (res.code === 0) {
      ffmpeg = true;
    }
  } catch {
    ffmpeg = false;
  }

  return { ytDlp, ytDlpVersion, ffmpeg };
}

// HTTP Request Handler
async function handleHttp(req: Request): Promise<Response> {
  const url = new URL(req.url);

  // Health check endpoint
  if (url.pathname === "/api/health") {
    const tools = await checkTools();
    return Response.json(tools);
  }

  // Folder browser endpoint
  if (url.pathname === "/api/browse" && req.method === "POST") {
    const selected = await pickFolder();
    return Response.json({ path: selected });
  }

  // SSE Download endpoint
  if (url.pathname === "/api/download") {
    const targetUrl = url.searchParams.get("url");
    const savePath = url.searchParams.get("savePath") || Deno.cwd();

    if (!targetUrl) {
      return new Response("Missing url parameter", { status: 400 });
    }

    const stream = new ReadableStream({
      async start(controller) {
        const send = (data: Record<string, unknown>) => {
          const payload = `data: ${JSON.stringify(data)}\n\n`;
          controller.enqueue(new TextEncoder().encode(payload));
        };

        send({ status: "starting", message: "Startar nedladdning..." });

        try {
          const cmd = new Deno.Command("yt-dlp", {
            args: [
              "-x",
              "--audio-format",
              "mp3",
              "--newline",
              "--progress",
              targetUrl,
            ],
            cwd: savePath,
            stdout: "piped",
            stderr: "piped",
          });

          const process = cmd.spawn();

          const readStream = async (readable: ReadableStream<Uint8Array>, isError = false) => {
            const reader = readable.getReader();
            const decoder = new TextDecoder();
            let buffer = "";

            while (true) {
              const { value, done } = await reader.read();
              if (done) break;
              buffer += decoder.decode(value, { stream: true });
              const lines = buffer.split("\n");
              buffer = lines.pop() || "";

              for (const line of lines) {
                const trimmed = line.trim();
                if (!trimmed) continue;

                const match = trimmed.match(
                  /\[download\]\s+([\d.]+)%\s+of\s+~?([\d.]+\w+)\s+at\s+([\d.]+\w+\/s)\s+ETA\s+([\d:]+)/
                );
                if (match) {
                  send({
                    status: "downloading",
                    percent: parseFloat(match[1]),
                    speed: match[3],
                    eta: match[4],
                    message: trimmed,
                  });
                } else if (trimmed.includes("[ExtractAudio]") || trimmed.includes("[ffmpeg]")) {
                  send({
                    status: "converting",
                    percent: 99,
                    message: "Konverterar till MP3...",
                  });
                } else {
                  send({
                    status: isError ? "error" : "downloading",
                    message: trimmed,
                  });
                }
              }
            }
          };

          const [, , exitStatus] = await Promise.all([
            readStream(process.stdout),
            readStream(process.stderr, false),
            process.status,
          ]);

          if (exitStatus.code === 0) {
            send({ status: "finished", percent: 100, message: "Klar!" });
          } else {
            send({ status: "error", message: `yt-dlp avslutades med kod ${exitStatus.code}` });
          }
        } catch (err) {
          send({ status: "error", message: (err as Error).message || "Nedladdningsfel" });
        } finally {
          controller.close();
        }
      },
    });

    return new Response(stream, {
      headers: {
        "Content-Type": "text/event-stream",
        "Cache-Control": "no-cache",
        "Connection": "keep-alive",
      },
    });
  }

  // Serve static UI assets from ./dist with SPA fallback using Deno standard library
  try {
    const res = await serveDir(req, {
      fsRoot: "dist",
      quiet: true,
    });
    if (res.status === 404) {
      return await serveFile(req, "dist/index.html");
    }
    return res;
  } catch {
    return new Response("Frontend not built. Run 'deno task build' first.", { status: 404 });
  }
}

// Start Server / Deno Desktop Host
console.log(`Video-Piper host running at http://localhost:${PORT}`);
Deno.serve({ port: PORT }, handleHttp);
