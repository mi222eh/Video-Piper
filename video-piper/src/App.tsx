import { ReactNode, useEffect, useState } from "react";
import "./App.css";
import { Label } from "./components/ui/label";
import { Input } from "./components/ui/input";
import { Button } from "./components/ui/button";
import { open } from "@tauri-apps/plugin-dialog"
import { readText } from "@tauri-apps/plugin-clipboard-manager"
import { Command, } from "@tauri-apps/plugin-shell"


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
      <div className="flex flex-col items-center justify-center gap-4 p-4">
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
        <Button className="mt-4" onClick={handleDownload} disabled={isLoading}>
          {isLoading ? "Laddar ner..." : "Ladda ner MP3"}
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
