import { join } from "path";
import { mkdirpSync, writeFile, mkdirp, createFile, appendFile } from "fs-extra";
import { date } from "quasar";
const src = {
    getLogPath(): string{
        return join('./', 'AppData', 'log');
    },
    getLogFileToWrite(): string{
        const dateString:string = date.formatDate(new Date(), 'YYYY-MM-D HH_mm_ss')
        return join(this.getLogPath(), `${dateString}.txt`);
    },
    getErrorLogFileToWrite(): string{
        const dateString:string = date.formatDate(new Date(), 'YYYY-MM-D HH_mm_ss')
        return join(this.getLogPath(), `[ERROR]${dateString}.txt`);
    }
}

export async function logErrorTexts(logTextList: string[]){
    console.log('ERROR TEXT', logTextList);
    const logFile = src.getErrorLogFileToWrite();
    await createFile(logFile);
    // await mkdirp(logFile);
    for (const text of logTextList) {
        await appendFile(logFile, text + '\r\n');
    }
}



mkdirpSync(src.getLogPath());