import { execFile, exec } from 'child_process';
import { join, basename, dirname, resolve, } from 'path';
import {CommandArguments, CommandFilename, CommandFormat, CurrentWorkingDirectory, FinishedListener, CommandURL} from './types/ExecutionTypes';
import { move, pathExists, exists, existsSync, createWriteStream, mkdirp, writeFile, readFile} from 'fs-extra';
import { get } from 'https';
import {  getYoutubeDlLatestReleaseTag } from './github/github';

const settings = {
    contentFolder: () => resolve(join('.', 'AppData', 'lib', 'youtube-dl')),
    youtubeDlSrc: () => join(settings.contentFolder(), 'youtube-dl.exe'),
    versionFile: () => join(settings.contentFolder(), 'version.txt'),
    latestUpdatedFile: () => join(settings.contentFolder(), 'lastCheck.txt'),
    updateCheckInterval: 1000 * 60 * 60 * 24 * 7
}
interface ExecuteOptions extends CurrentWorkingDirectory, CommandURL, CommandArguments, FinishedListener{}
function execute({
    cwd = '',
    url,
    args,
    finishedListener,
}: ExecuteOptions) {
    console.log(settings.youtubeDlSrc());
    return execFile(
        settings.youtubeDlSrc(),
        [...args, url],
        {
            cwd: cwd,
            maxBuffer: 1024 * 1024 * 10
        },
        finishedListener
    );
}
interface GetPlaylistOptions extends CommandURL, FinishedListener{}
export function getPlaylistInfo({ url, finishedListener = () => '' }: GetPlaylistOptions) {
    const args: string[] = ['-J', '--yes-playlist', '--flat-playlist', '-s'];
    return execute({ url, args, finishedListener });
}

interface GetInfoOptions extends CommandURL, CommandFormat, FinishedListener{}
export function getInfo({ url, format, finishedListener = () => '' }:GetInfoOptions) {
    let args: string[] = [];
    if (format) {
        args = [...args, '-f', format];
    }
    args = [...args, '--print-json', '--no-playlist', '-s'];
    return execute({  url, args, finishedListener });
}

interface StreamVideoIntoFileOptions extends CurrentWorkingDirectory, CommandURL, CommandFilename, CommandFormat, FinishedListener{}
export function streamVideoIntoFile({
    cwd,
    url,
    filename,
    format,
    finishedListener = () => ''
}:StreamVideoIntoFileOptions) {
    if (typeof filename !== 'string') {
        throw new Error('Filename required');
    }
    if (typeof url !== 'string') {
        throw new Error('url required');
    }
    if (typeof cwd !== 'string') {
        throw new Error('cwd required');
    }
    let args: string[] = [];
    if (format) {
        args = [...args, '-f', format];
    }
    args = [...args, '-o', filename, '--no-part', '--mark-watched'];
    return execute({ cwd, url, args, finishedListener });
}

export function youtubeDlExists(){
    return new Promise((resolve) => {
        exists(settings.youtubeDlSrc(), exists => resolve(exists));
    });
}

export function GetYoutubeDLDownloadLink(start:string): Promise<string>{
    return new Promise((resolve, reject) => {
        (function GoNext(link:string){
            get(link, res => {
                if(!res.headers.location){
                    resolve(link);
                    return;
                } 
                GoNext(res.headers.location);
            });
        }(start))
    });
}
export async function writeCurrentDateInVersionFile(){
    await writeFile(settings.latestUpdatedFile(), new Date().toUTCString());
}
export async function downloadYoutubeDl(){
    const version = await getYoutubeDlLatestReleaseTag();
    const link = await GetYoutubeDLDownloadLink('https://youtube-dl.org/downloads/latest/youtube-dl.exe');
    const file = createWriteStream(settings.youtubeDlSrc());

    await new Promise((resolve, reject) => {
        get(link, res => {
            const stream = res.pipe(file);
            stream.on('close', () => {
                file.close();
                resolve();
            })
        });
    });
    file.close();

    await writeFile(settings.versionFile(), version);
    await writeCurrentDateInVersionFile();
}



export async function GetCurrentVersion(){
    const fileExists = await new Promise((resolve, reject) => {
        exists(settings.versionFile(), (exists) => resolve(exists));
    });
    if(!fileExists){
        return '';
    }
    return await readFile(settings.versionFile());
}

export async function isTimeToDownload(){
    const fileExists = await new Promise((resolve, reject) => {
        exists(settings.latestUpdatedFile(), (exists) => resolve(exists));
    });
    if(!fileExists){
        return true;
    }
    const dateString: string = await (await readFile(settings.latestUpdatedFile())).toString();
    const date = new Date(dateString);

    const isTime = (Number(new Date()) - Number(date)) > settings.updateCheckInterval;
    console.log(isTime);
    
    return (Number(new Date()) - Number(date)) > settings.updateCheckInterval;
}

export async function checkYoutubeDl(){
    console.count('YT');
    await mkdirp(settings.contentFolder());
    if(! await youtubeDlExists()){
        await downloadYoutubeDl();
        return;
    }
    console.count('YT');
    if(!await isTimeToDownload()){
        return;
    }
    console.count('YT');
    const version = await getYoutubeDlLatestReleaseTag();
    const currentVersion = await GetCurrentVersion();
    if(version == currentVersion){
        await writeCurrentDateInVersionFile()
        return;
    }
    await downloadYoutubeDl();
}
