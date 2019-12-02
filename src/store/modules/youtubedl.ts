import { execFile } from 'child_process';
import { join } from 'path';
import {CommandArguments, CommandFilename, CommandFormat, CurrentWorkingDirectory, FinishedListener, CommandURL} from './types/ExecutionTypes';


const getPath = () => join(__statics, 'bin', 'youtube-dl.exe');

interface ExecuteOptions extends CurrentWorkingDirectory, CommandURL, CommandArguments, FinishedListener{}
function execute({
    cwd = '',
    url,
    args,
    finishedListener,
}: ExecuteOptions) {
    return execFile(
        getPath(),
        [...args, url],
        {
            cwd: cwd,
            maxBuffer: 1024 * 1024 * 10
        },
        finishedListener
    );
}
interface GetPlaylistOptions extends CommandURL, FinishedListener{}
function getPlaylistInfo({ url, finishedListener = () => '' }: GetPlaylistOptions) {
    const args: string[] = ['-J', '--yes-playlist', '-s'];
    return execute({ url, args, finishedListener });
}

interface GetInfoOptions extends CommandURL, CommandFormat, FinishedListener{}
function getInfo({ url, format, finishedListener = () => '' }:GetInfoOptions) {
    let args: string[] = [];
    if (format) {
        args = [...args, '-f', format];
    }
    args = [...args, '--print-json', '--no-playlist', '-s'];
    return execute({  url, args, finishedListener });
}

interface StreamVideoIntoFileOptions extends CurrentWorkingDirectory, CommandURL, CommandFilename, CommandFormat, FinishedListener{}
function streamVideoIntoFile({
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
    args = [...args, '-o', filename, '--no-part'];
    return execute({ cwd, url, args, finishedListener });
}

export default {
    getInfo,
    getPlaylistInfo,
    streamVideoIntoFile
};
