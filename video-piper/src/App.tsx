import { ReactNode, useEffect, useState } from "react";
import "./App.css";
import { Label } from "./components/ui/label";
import { Input } from "./components/ui/input";
import { Button } from "./components/ui/button";
import { Progress } from "./components/ui/progress";
import { browseDirectory, checkSystem, downloadAudio, DownloadProgress, readClipboard } from "./lib/api";
import { CheckCircle2, Download, FolderOpen, Loader2, Music, Sparkles } from "lucide-react";

const useSaveLocationLocalStorage = (key: string, initialValue: string) => {
  const [value, setValue] = useState<string>(() => {
    const storedValue = localStorage.getItem(key);
    return storedValue !== null ? storedValue : initialValue;
  });
  useEffect(() => {
    localStorage.setItem(key, value);
  }, [key, value]);
  return [value, setValue] as const;
};

function App() {
  const [link, setLink] = useState<string>("");
  const [savePath, setSavePath] = useSaveLocationLocalStorage("savePath", "");
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [progress, setProgress] = useState<DownloadProgress | null>(null);
  const [systemStatus, setSystemStatus] = useState<{ ytDlp: boolean; ytDlpVersion?: string } | null>(null);

  useEffect(() => {
    checkSystem().then((status) => setSystemStatus(status));
  }, []);

  async function handleBrowse() {
    const dir = await browseDirectory();
    if (dir) {
      setSavePath(dir);
    }
  }

  async function handlePaste() {
    const text = await readClipboard();
    if (text) {
      setLink(text.trim());
    }
  }

  async function handleDownload() {
    if (!link.trim()) return;

    setIsLoading(true);
    setProgress({ status: "starting", percent: 0, message: "Initierar..." });

    try {
      await downloadAudio(link, savePath, (p) => {
        setProgress(p);
      });
      setProgress({ status: "finished", percent: 100, message: "Nedladdning slutförd!" });
      setLink("");
    } catch (err) {
      setProgress({
        status: "error",
        message: (err as Error).message || "Kunde inte ladda ner",
      });
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <AppContainer>
      <div className="w-full max-w-2xl mx-auto flex flex-col gap-6 p-6">
        {/* Header */}
        <div className="flex items-center justify-between border-b pb-4">
          <div className="flex items-center gap-2">
            <div className="p-2 rounded-xl bg-primary text-primary-foreground">
              <Music className="w-5 h-5" />
            </div>
            <div>
              <h1 className="text-xl font-bold tracking-tight">Video Piper</h1>
              <p className="text-xs text-muted-foreground">Deno Edition • Snabb MP3-konverterare</p>
            </div>
          </div>
          {systemStatus?.ytDlp && (
            <span className="text-xs text-muted-foreground bg-muted px-2.5 py-1 rounded-full flex items-center gap-1.5">
              <span className="w-2 h-2 rounded-full bg-green-500 animate-pulse" />
              yt-dlp v{systemStatus.ytDlpVersion || "aktiv"}
            </span>
          )}
        </div>

        {/* Inputs */}
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="youtube-url" className="text-sm font-medium">
              YouTube Länk
            </Label>
            <div className="flex gap-2">
              <Input
                id="youtube-url"
                value={link}
                placeholder="https://www.youtube.com/watch?v=..."
                type="text"
                className="grow text-sm font-mono"
                onChange={(e) => setLink(e.target.value)}
                disabled={isLoading}
              />
              <Button variant="outline" onClick={handlePaste} disabled={isLoading} className="shrink-0">
                <Sparkles className="w-4 h-4 mr-1.5" />
                Klistra in
              </Button>
            </div>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="save-path" className="text-sm font-medium">
              Spara till mapp
            </Label>
            <div className="flex gap-2">
              <Input
                id="save-path"
                value={savePath || "Standard / Arbetskatalog"}
                readOnly
                disabled={isLoading}
                className="grow text-sm font-mono text-muted-foreground bg-muted/40"
              />
              <Button variant="outline" onClick={handleBrowse} disabled={isLoading} className="shrink-0">
                <FolderOpen className="w-4 h-4 mr-1.5" />
                Bläddra
              </Button>
            </div>
          </div>
        </div>

        {/* Progress & Status */}
        {progress && (
          <div className="space-y-2 p-3.5 bg-muted/40 rounded-lg border text-sm">
            <div className="flex justify-between items-center text-xs text-muted-foreground">
              <span className="font-medium text-foreground">
                {progress.status === "downloading" && `Laddar ner ${progress.percent ? `(${progress.percent}%)` : ""}`}
                {progress.status === "converting" && "Konverterar ljud..."}
                {progress.status === "finished" && "Slutförd!"}
                {progress.status === "error" && "Ett fel uppstod"}
              </span>
              {progress.speed && <span>{progress.speed}</span>}
            </div>

            {progress.percent !== undefined && (
              <Progress value={progress.percent} className="h-2" />
            )}

            {progress.message && (
              <p className="text-xs text-muted-foreground truncate">{progress.message}</p>
            )}
          </div>
        )}

        {/* Action Button */}
        <Button
          size="lg"
          className="w-full font-semibold"
          onClick={handleDownload}
          disabled={isLoading || !link.trim()}
        >
          {isLoading ? (
            <>
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              Laddar ner & konverterar...
            </>
          ) : progress?.status === "finished" ? (
            <>
              <CheckCircle2 className="w-4 h-4 mr-2" />
              Ladda ner en till
            </>
          ) : (
            <>
              <Download className="w-4 h-4 mr-2" />
              Ladda ner MP3
            </>
          )}
        </Button>
      </div>
    </AppContainer>
  );
}

type Theme = "light" | "dark";

function AppContainer(props: { children: ReactNode }) {
  const systemDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
  const [theme] = useState<Theme>(
    () => (localStorage.getItem("theme") as Theme) || (systemDark ? "dark" : "light")
  );

  useEffect(() => {
    const root = window.document.documentElement;
    root.classList.remove("light", "dark");
    root.classList.add(theme);
  }, [theme]);

  return <div className="min-h-screen bg-background text-foreground flex items-center justify-center">{props.children}</div>;
}

export default App;
