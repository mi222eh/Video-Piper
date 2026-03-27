import { ReactNode, useEffect, useState } from "react";
import "./App.css";
import { Label } from "./components/ui/label";
import { Input } from "./components/ui/input";
import { Button } from "./components/ui/button";
import { Alert, AlertDescription, AlertTitle } from "./components/ui/alert";
import { open } from "@tauri-apps/plugin-dialog"
import { readText } from "@tauri-apps/plugin-clipboard-manager"
import { Command, } from "@tauri-apps/plugin-shell"


const AUTO_UPDATE_CHECK_COOLDOWN_MS = 24 * 60 * 60 * 1000;
const LAST_AUTO_UPDATE_CHECK_KEY = "ytDlpLastAutoUpdateCheckAt";

type YtDlpUpdateStatus = {
  currentVersion: string;
  latestVersion: string;
}

const useSaveLocationLocalStorage = (key: string, initialValue: string) => {
  const [value, setValue] = useState<string>(() => {
    const storedValue = localStorage.getItem(key);
    return storedValue !== null ? storedValue : initialValue;
  });
  useEffect(() => {
    localStorage.setItem(key, value);
  }, [key, value]);
  return [value, setValue] as const;
}

function App() {

  const [link, setLink] = useState<string>("");
  const [savePath, setSavePath] = useSaveLocationLocalStorage("savePath", "");
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [isCheckingUpdate, setIsCheckingUpdate] = useState<boolean>(false);
  const [isUpdatingYtDlp, setIsUpdatingYtDlp] = useState<boolean>(false);
  const [updateStatus, setUpdateStatus] = useState<YtDlpUpdateStatus | null>(null);

  const isBusy = isLoading || isCheckingUpdate || isUpdatingYtDlp;

  function compareVersions(currentVersion: string, latestVersion: string) {
    const parseVersion = (value: string) =>
      value
        .replace(/^v/i, "")
        .split(".")
        .map((part) => Number.parseInt(part, 10))
        .filter((part) => Number.isFinite(part));

    const currentParts = parseVersion(currentVersion);
    const latestParts = parseVersion(latestVersion);
    const maxLength = Math.max(currentParts.length, latestParts.length);

    for (let index = 0; index < maxLength; index += 1) {
      const currentPart = currentParts[index] ?? 0;
      const latestPart = latestParts[index] ?? 0;
      if (currentPart !== latestPart) {
        return currentPart - latestPart;
      }
    }

    return 0;
  }

  function getVersion(output: string) {
    return output
      .split(/\r?\n/)
      .map((line) => line.trim())
      .find((line) => line.length > 0) ?? "";
  }

  async function runYtCommand(args: string[], cwd?: string) {
    return new Promise<{ code: number | null, stdout: string, stderr: string }>((resolve, reject) => {
      const cmd = Command.create("yt", args, cwd ? { cwd } : undefined);
      let stdout = "";
      let stderr = "";

      cmd.stdout.on("data", (line: string) => {
        stdout += `${line}\n`;
      });
      cmd.stderr.on("data", (line: string) => {
        stderr += `${line}\n`;
      });
      cmd.on("close", (payload: { code: number | null }) => {
        resolve({ code: payload.code, stdout, stderr });
      });
      cmd.spawn().catch(reject);
    });
  }

  async function checkForUpdates() {
    setIsCheckingUpdate(true);
    try {
      const [versionResult, latestReleaseResponse] = await Promise.all([
        runYtCommand(["--version"]),
        fetch("https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest", {
          headers: {
            Accept: "application/vnd.github+json",
          },
        }),
      ]);

      if (versionResult.code !== 0) {
        return;
      }

      const currentVersion = getVersion(versionResult.stdout || versionResult.stderr);
      if (!currentVersion || !latestReleaseResponse.ok) {
        return;
      }

      const latestRelease = await latestReleaseResponse.json() as { tag_name?: string };
      const latestVersion = latestRelease.tag_name?.replace(/^v/i, "") ?? "";
      if (!latestVersion) {
        return;
      }

      if (compareVersions(currentVersion, latestVersion) < 0) {
        setUpdateStatus({ currentVersion, latestVersion });
      } else {
        setUpdateStatus(null);
      }
    } finally {
      setIsCheckingUpdate(false);
    }
  }

  async function maybeCheckForUpdatesAfterFailure() {
    const lastCheckedValue = Number.parseInt(localStorage.getItem(LAST_AUTO_UPDATE_CHECK_KEY) ?? "", 10);
    const now = Date.now();

    if (Number.isFinite(lastCheckedValue) && now - lastCheckedValue < AUTO_UPDATE_CHECK_COOLDOWN_MS) {
      return;
    }

    localStorage.setItem(LAST_AUTO_UPDATE_CHECK_KEY, now.toString());
    await checkForUpdates();
  }

  async function handleUpdateYtDlp() {
    setIsUpdatingYtDlp(true);
    try {
      const updateResult = await runYtCommand(["-U"]);
      if (updateResult.code === 0) {
        await checkForUpdates();
      }
    } finally {
      setIsUpdatingYtDlp(false);
    }
  }

  async function handleBrowse() {
    const dir = await open({
      directory: true,
    })
    setSavePath(dir ?? "");
  }

  async function handlePaste() {
    const text = await readText();
    console.log(text);
    setLink(text);
  }

  async function handleDownload() {
    console.log("Downloading...");
    setIsLoading(true);
    try {
      const parsedLink = new URL(link);
      const linkToUse = new URL(parsedLink.origin);
      linkToUse.pathname = parsedLink.pathname;
      linkToUse.searchParams.set("v", parsedLink.searchParams.get("v") || "");

      const downloadResult = await runYtCommand(["-t", "mp3", linkToUse.toString()], savePath);
      if (downloadResult.code !== 0) {
        await maybeCheckForUpdatesAfterFailure();
      }
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <AppContainer>
      <div className="flex flex-col items-center justify-center gap-4 p-4">
        {updateStatus && (
          <Alert className="w-full max-w-3xl">
            <AlertTitle>Ny yt-dlp uppdatering hittad</AlertTitle>
            <AlertDescription className="flex flex-wrap items-center justify-between gap-2">
              <span>
                Installerad version: {updateStatus.currentVersion} · Senaste version: {updateStatus.latestVersion}
              </span>
              <Button onClick={handleUpdateYtDlp} disabled={isBusy}>
                {isUpdatingYtDlp ? "Uppdaterar..." : "Uppdatera yt-dlp"}
              </Button>
            </AlertDescription>
          </Alert>
        )}
        <Label className="text-nowrap grow flex flex-row">
          <p>Youtube Länk:</p>
          <Input value={link} type="text" className="grow w-xl" onChange={e => setLink(e.target.value)} />
          <Button className="ml-2" onClick={handlePaste}>Klistra in</Button>
        </Label>
        <Label className="text-nowrap grow flex flex-row">
          <p>Spara till:</p>
          <Input disabled value={savePath} readOnly className="grow w-xl" />
          <Button className="ml-2" onClick={handleBrowse}>Bläddra</Button>
        </Label>
        <div className="mt-4 flex gap-2">
          <Button onClick={handleDownload} disabled={isBusy}>
          {isLoading ? "Laddar ner..." : "Ladda ner MP3"}
          </Button>
          <Button variant="secondary" onClick={checkForUpdates} disabled={isBusy}>
            {isCheckingUpdate ? "Kollar..." : "Kolla uppdateringar"}
          </Button>
        </div>
      </div>
    </AppContainer>
  );
}

type Theme = "light" | "dark"

function AppContainer(props: { children: ReactNode }) {

  const sytemDark = window.matchMedia("(prefers-color-scheme: dark)").matches

  const [theme] = useState<Theme>(
    () => localStorage.getItem("theme") as Theme || (sytemDark ? "dark" : "light")
  )

  useEffect(() => {
    const root = window.document.documentElement
    root.classList.remove("light", "dark")
    root.classList.add(theme)
  }, [theme])


  return <div className="w-screen h-screen">
    {props.children}
  </div>
}

export default App;
