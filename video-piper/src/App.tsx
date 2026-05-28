import { ReactNode, useEffect, useState } from "react";
import "./App.css";
import { Label } from "./components/ui/label";
import { Input } from "./components/ui/input";
import { Button } from "./components/ui/button";
import { Alert, AlertDescription, AlertTitle } from "./components/ui/alert";
import { open } from "@tauri-apps/plugin-dialog"
import { readText } from "@tauri-apps/plugin-clipboard-manager"
import { Command, } from "@tauri-apps/plugin-shell"

const YT_DLP_WINGET_ID = "yt-dlp.yt-dlp";

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

const includesAny = (value: string, candidates: string[]) => candidates.some(candidate => value.includes(candidate));

const detectMissingPackage = (output: string) => includesAny(output, [
  "no installed package found",
  "no package found matching input criteria",
  "inga installerade paket hittades",
  "hittades inga installerade paket",
]);

const detectNoUpdates = (output: string) => includesAny(output, [
  "no available upgrade found",
  "no newer package versions are available",
  "inga tillgängliga uppgraderingar hittades",
  "inga nyare paketversioner är tillgängliga",
]);

function App() {

  const [link, setLink] = useState<string>("");
  const [savePath, setSavePath] = useSaveLocationLocalStorage("savePath", "");
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [isCheckingYtDlp, setIsCheckingYtDlp] = useState<boolean>(true);
  const [isUpdatingYtDlp, setIsUpdatingYtDlp] = useState<boolean>(false);
  const [isYtDlpMissing, setIsYtDlpMissing] = useState<boolean>(false);
  const [isYtDlpUpdateAvailable, setIsYtDlpUpdateAvailable] = useState<boolean>(false);
  const [ytDlpStatusMessage, setYtDlpStatusMessage] = useState<string>("");
  const [ignoreMissingYtDlp, setIgnoreMissingYtDlp] = useState<boolean>(false);

  async function checkYtDlpStatus() {
    setIsCheckingYtDlp(true);
    setYtDlpStatusMessage("");

    try {
      const listResult = await Command.create("winget", [
        "list",
        "--id",
        YT_DLP_WINGET_ID,
        "--exact",
        "--accept-source-agreements",
      ]).execute();

      const listOutput = `${listResult.stdout}\n${listResult.stderr}`.toLowerCase();
      const missingPackage = listResult.code !== 0 || detectMissingPackage(listOutput);

      if (missingPackage) {
        setIsYtDlpMissing(true);
        setIsYtDlpUpdateAvailable(false);
        setYtDlpStatusMessage("yt-dlp hittades inte via winget. Installera yt-dlp för att använda appen.");
        return;
      }

      setIsYtDlpMissing(false);
      setIgnoreMissingYtDlp(false);

      const upgradeResult = await Command.create("winget", [
        "upgrade",
        "--id",
        YT_DLP_WINGET_ID,
        "--exact",
        "--accept-source-agreements",
      ]).execute();

      const upgradeOutput = `${upgradeResult.stdout}\n${upgradeResult.stderr}`.toLowerCase();
      const hasUpdate = !detectNoUpdates(upgradeOutput) && upgradeOutput.includes("yt-dlp");

      setIsYtDlpUpdateAvailable(hasUpdate);

      if (!hasUpdate) {
        setYtDlpStatusMessage("");
      }
    } catch {
      setYtDlpStatusMessage("Kunde inte kontrollera yt-dlp via winget.");
      setIsYtDlpMissing(false);
      setIsYtDlpUpdateAvailable(false);
    } finally {
      setIsCheckingYtDlp(false);
    }
  }

  useEffect(() => {
    void checkYtDlpStatus();
  }, []);

  async function handleBrowse() {
    if (controlsDisabled) return;
    const dir = await open({
      directory: true,
    })
    setSavePath(dir ?? "");
  }

  async function handlePaste() {
    if (controlsDisabled) return;
    const text = await readText();
    console.log(text);
    setLink(text);
  }

  async function handleYtDlpUpdate() {
    setIsUpdatingYtDlp(true);
    setYtDlpStatusMessage("");

    try {
      const result = await Command.create("winget", [
        "upgrade",
        "--id",
        YT_DLP_WINGET_ID,
        "--exact",
        "--accept-source-agreements",
        "--accept-package-agreements",
      ]).execute();

      if (result.code !== 0) {
        setYtDlpStatusMessage("Kunde inte uppdatera yt-dlp via winget.");
      }
    } catch {
      setYtDlpStatusMessage("Kunde inte uppdatera yt-dlp via winget.");
    } finally {
      setIsUpdatingYtDlp(false);
      await checkYtDlpStatus();
    }
  }

  const controlsDisabled = isLoading || isUpdatingYtDlp || isCheckingYtDlp || (isYtDlpMissing && !ignoreMissingYtDlp);

  async function handleDownload() {
    if (controlsDisabled) return;
    console.log("Downloading...");
    setIsLoading(true);
    try {
      await new Promise(async (res, rej) => {

        const parsedLink = new URL(link);
        const linkToUse = new URL(parsedLink.origin);
        linkToUse.pathname = parsedLink.pathname;
        linkToUse.searchParams.set("v", parsedLink.searchParams.get("v") || "");

        const cmd = Command.create("yt", ["-t", "mp3", linkToUse.toString()], {
          cwd: savePath,
        })
        cmd.stdout.on("data", (line) => {
          console.log(`stdout: ${line}`);
        });
        cmd.stderr.on("data", (line) => {
          console.log(`stderr: ${line}`);
        });
        cmd.on("close", (payload) => {
          console.log(`child process exited with code ${payload.code}`);
          console.log(`child process exited with signal ${payload.signal}`);
          payload.code === 0 ? res(null) : rej();
        });
        await cmd.spawn();

      })
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <AppContainer>
      <div className="mx-auto flex h-full w-full max-w-4xl flex-col justify-center gap-4 p-4">
        {isYtDlpUpdateAvailable && (
          <Alert className="border-amber-300 bg-amber-50 text-amber-950 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-100">
            <AlertTitle>Uppdatering tillgänglig för yt-dlp</AlertTitle>
            <AlertDescription className="w-full">
              <p>En ny version av yt-dlp finns i winget.</p>
              <Button className="mt-2" size="sm" onClick={handleYtDlpUpdate} disabled={isUpdatingYtDlp || isLoading || isCheckingYtDlp}>
                {isUpdatingYtDlp ? "Uppdaterar..." : "Uppdatera yt-dlp"}
              </Button>
            </AlertDescription>
          </Alert>
        )}
        {isYtDlpMissing && !ignoreMissingYtDlp && (
          <Alert variant="destructive">
            <AlertTitle>yt-dlp saknas</AlertTitle>
            <AlertDescription className="w-full">
              <p>{ytDlpStatusMessage}</p>
              <div className="mt-2 flex gap-2">
                <Button size="sm" variant="outline" onClick={() => setIgnoreMissingYtDlp(true)}>
                  Ignorera
                </Button>
              </div>
            </AlertDescription>
          </Alert>
        )}
        {ytDlpStatusMessage && !isYtDlpMissing && !isYtDlpUpdateAvailable && (
          <Alert>
            <AlertTitle>Status</AlertTitle>
            <AlertDescription>{ytDlpStatusMessage}</AlertDescription>
          </Alert>
        )}
        <Label className="text-nowrap grow flex flex-row">
          <p>Youtube Länk:</p>
          <Input value={link} type="text" className="grow w-xl" onChange={e => setLink(e.target.value)} disabled={controlsDisabled} />
          <Button className="ml-2" onClick={handlePaste} disabled={controlsDisabled}>Klistra in</Button>
        </Label>
        <Label className="text-nowrap grow flex flex-row">
          <p>Spara till:</p>
          <Input disabled value={savePath} readOnly className="grow w-xl" />
          <Button className="ml-2" onClick={handleBrowse} disabled={controlsDisabled}>Bläddra</Button>
        </Label>
        <Button className="mt-4" onClick={handleDownload} disabled={controlsDisabled}>
          {isCheckingYtDlp ? "Kontrollerar yt-dlp..." : isLoading ? "Laddar ner..." : "Ladda ner MP3"}
        </Button>
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
