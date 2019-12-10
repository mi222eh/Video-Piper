import { Context, VideoTask, VideoTaskInfo, VideoTaskStatus } from './mediaManagerTypes';
import youtubeDl from '../modules/youtubedl';
import ffmpeg from '../modules/ffmpeg';
import { formatDuration, TrackProgress } from '../modules/helpers';

import sanitizeName from 'sanitize-filename';
import fs from 'fs-extra';
import path from 'path';
import terminate from 'terminate';
import { VideoInfo } from '../types/Video/VideoInfo';
import { ChildProcess } from 'child_process';

const _ProcessBank : {[key:string]: ChildProcess | null | undefined} = {};


// PRIVATE FUNCTIONS
export async function getVideoFormatInfo(
    context: Context,
    opts: { url: string; format: string }
) {
    console.log(opts);
    const json: string = await new Promise((resolve, reject) => {
        youtubeDl.getInfo({
            url: opts.url,
            format: opts.format || '',
            finishedListener(err, out) {
                if (err) {
                    reject(err);
                    return;
                }
                resolve(out);
            }
        });
    });
    return JSON.parse(json);
}

export async function downloadMedia(context: Context, task: VideoTask) {
    const formats = task.info.chosenFormat.format_id.split('+');
    if (formats.length <= task.info.tempFileNames.length) {
        return;
    }
    /**
     * @type {VideoInfo}
     */
    const info: VideoInfo = await getVideoFormatInfo(context, {
        format: formats[task.info.tempFileNames.length],
        url: task.info.URL
    });
    const tempFilename = `${task.info.tempFileNames.length}.${info.ext}`;
    const tracker = TrackProgress({
        file: path.join(task.info.tempFolder, tempFilename),
        size: 0,
        totalSize: info.filesize || -1,
        listener(percentage) {
            context.commit('TaskUpdateInformation', {
                id: task.info.id,
                task:{percentage}
            });
        }
    });
    await new Promise((resolve, reject) => {
        const process = youtubeDl.streamVideoIntoFile({
            cwd: task.info.tempFolder,
            filename: tempFilename,
            url: task.info.URL,
            format: info.format_id,
            finishedListener(err, out) {
                tracker.close();
                _ProcessBank[task.info.id] = null;
                if (task.isStopped) {
                    reject();
                    return;
                }
                context.commit('TASKSetPercentage', {
                    id: task.info.id,
                    percentage: 1
                });

                if (err) {
                    reject(err);
                } else {
                    resolve(out);
                }
            }
        });
        _ProcessBank[task.info.id] = process;
    });

    context.commit('TASKAddTempFilename', {
        id: task.info.id,
        filename: tempFilename
    });
}
export async function combineMedia(context: Context, task: VideoTask) {
    const tempFileName = `${task.info.tempFileNames.join('')}.${
        task.info.chosenFormat.ext
    }`;
    console.log("COMBINE", task, tempFileName);
    
    await fs.remove(path.join(task.info.tempFolder, tempFileName));
    const tracker = TrackProgress({
        file: path.join(task.info.tempFolder, tempFileName),
        size: 0,
        totalSize: task.info.chosenFormat.filesize || -1,
        listener(percentage) {
            context.commit('TASKSetPercentage', {
                id: task.info.id,
                percentage
            });
        }
    });
    await new Promise((resolve, reject) => {
        const process = ffmpeg.combine({
            cwd: task.info.tempFolder,
            inputs: task.info.tempFileNames,
            output: tempFileName,
            finishedListener(err, out) {
                tracker.close();
                _ProcessBank[task.info.id] = null;
                if (task.isStopped) {
                    reject();
                    return;
                }
                context.commit('TASKSetPercentage', {
                    id: task.info.id,
                    percentage: 1
                });
                if (err) {
                    reject(err);
                    return;
                }
                resolve(out);
            }
        });
        _ProcessBank[task.info.id] = process;
    });
    context.commit('TASKAddTempFilename', {
        id: task.info.id,
        filename: tempFileName
    });
}
export async function moveFinalFile(task: VideoTask) {
    const lastFileName =
        task.info.tempFileNames[task.info.tempFileNames.length - 1];
    const filename = sanitizeName(
        `${task.info.videoInfo.fulltitle}-${task.info.chosenFormat.height ||
            ''}.${task.info.chosenFormat.ext}`
    );
    await fs.move(
        path.join(task.info.tempFolder, lastFileName),
        path.join(task.info.folder, filename),
        {
            overwrite: true
        }
    );
}
export async function convertMedia(context: Context, task: VideoTask) {
    const last = task.info.tempFileNames[task.info.tempFileNames.length - 1];
    const tempFileName = `${last}.${task.info.chosenFormat.ext}`;
    await fs.remove(path.join(task.info.tempFolder, tempFileName));
    const tracker = TrackProgress({
        file: path.join(task.info.tempFolder, tempFileName),
        size: 0,
        totalSize: task.info.chosenFormat.filesize || -1,
        listener(percentage) {
            context.commit('TASKSetPercentage', {
                id: task.info.id,
                percentage
            });
        }
    });
    await new Promise((resolve, reject) => {
        const process = ffmpeg.convert({
            cwd: task.info.tempFolder,
            input: last,
            output: tempFileName,
            finishedListener(err, out) {
                tracker.close();
                if (task.isStopped) {
                    reject();
                    return;
                }
                _ProcessBank[task.info.id] = null;
                context.commit('TASKSetPercentage', {
                    id: task.info.id,
                    percentage: 1
                });
                if (err) {
                    reject(err);
                    return;
                }
                resolve(out);
            }
        });
        _ProcessBank[task.info.id] = process;
    });
    context.commit('TASKAddTempFilename', {
        id: task.info.id,
        filename: tempFileName
    });
}

async function removeTempFolder(task: VideoTask) {
    try{
        await fs.remove(task.info.tempFolder);
    }
    catch(err){
        console.log("Temo folder doesn't exist", err);
    }
}

export async function getVideoInfo(context: Context, url: string) {
    let json: VideoInfo;
    try {
        json = await context.dispatch('getVideoFormatInfo', {
            url,
            format: 'bestvideo+bestaudio'
        });
    } catch (error) {
        try {
            json = await context.dispatch('getVideoFormatInfo', { url });
        } catch (error) {
            throw new Error('Video couldn\'t be loaded');
        }
    }
    json.durationFormatted = formatDuration(json.duration) || 'Unknown';
    return json;
}

export async function getPlaylistInfo(context:Context, url: string) {
    const json: string = await new Promise((resolve, reject) => {
        youtubeDl.getPlaylistInfo({
            url,
            finishedListener(err, out) {
                if (err) {
                    reject(err);
                    return;
                }
                resolve(out);
            }
        });
    });
    return JSON.parse(json);
}

export async function addTaskToQueue(
    context: Context,
    videoTaskInfo: VideoTaskInfo
) {
    if(context.state.videoQueue.find(task => task.info.id === videoTaskInfo.id)){
        throw new Error("Video is already in queue");
    }
    if (!videoTaskInfo.folder) {
        throw new Error('Please choose a folder');
    }
    if (!videoTaskInfo.chosenFormat) {
        throw new Error('Please choose a format');
    }
    /**
     * @type {VideoTask}
     */
    videoTaskInfo.tempFolder = path.join(
        videoTaskInfo.folder,
        videoTaskInfo.id
    );
    videoTaskInfo.tempFileNames = [];
    const task: VideoTask = {
        inProgress: false,
        info: videoTaskInfo,
        isFinished: false,
        isStopped: false,
        percentage: 0,
        status: VideoTaskStatus.ready,
        statusText: '',
        process: null
    };
    context.commit('addVideoTaskToQueue', task);
}
export async function performVideoTask(context: Context, task: VideoTask) {
    if (!task) {
        return;
    }
    context.commit('TASKSetInprogress', {
        id: task.info.id,
        inProgress: true
    });
    context.commit('TASKSetIsStopped', {
        id: task.info.id,
        inProgress: true
    });

    try {
        // PREPARING
        context.commit('TASKSetStatus', {
            id: task.info.id,
            status: 'preparing'
        });
        await fs.mkdirp(task.info.tempFolder);

        // DOWNLOADING
        context.commit('TASKSetStatus', {
            id: task.info.id,
            status: 'downloading'
        });
        const formats = task.info.chosenFormat.format_id.split('+');
        for (let i = 0; i < formats.length; i++) {
            await context.dispatch('downloadMedia', task);
        }

        // COMBINING
        if (task.info.tempFileNames.length === 2) {
            context.commit('TASKSetStatus', {
                id: task.info.id,
                status: 'combining'
            });
            await context.dispatch("combineMedia", task);
        }
        // CONVERTING
        const last =
            task.info.tempFileNames[task.info.tempFileNames.length - 1];
        if (path.extname(last) !== `.${task.info.chosenFormat.ext}`) {
            context.commit('TASKSetStatus', {
                id: task.info.id,
                status: 'converting'
            });
            await context.dispatch("convertMedia", task);
        }
        // MOVING FINISHED FILE
        await moveFinalFile(task);

        // REMOVE TEMP FOLDER
        await removeTempFolder(task);
        context.commit('TASKSetStatus', {
            id: task.info.id,
            status: 'done'
        });
        context.commit('TASKSetIsFinished', {
            id: task.info.id,
            isFinished: true
        });
        context.commit('TASKSetInprogress', {
            id: task.info.id,
            inProgress: false
        });
    } catch (error) {
        if (task.isStopped) {
            return;
        }
        context.commit('TASKSetStatus', {
            id: task.info.id,
            status: 'error'
        });
        context.commit('TASKSetIsFinished', {
            id: task.info.id,
            isFinished: false
        });
        context.commit('TASKSetInprogress', {
            id: task.info.id,
            inProgress: false
        });
        console.error(error);
    }
}
export async function getNextReadyTask(context: Context) {
    return context.state.videoQueue.find(task => task.status === 'ready');
}
export async function stopTask(context: Context, id: string) {
    const task = context.state.videoQueue.find(task => task.info.id === id);

    if (!task) {
        return;
    }



    context.commit('TASKSetStatus', {
        id: task.info.id,
        status: 'stopped'
    });
    context.commit('TASKSetIsStopped', {
        id: task.info.id,
        isStopped: true
    });

    await new Promise(resolve => {
        const process = _ProcessBank[task.info.id];
        if (process === null || process === undefined) {
            resolve()
            return;
        }
        terminate(process.pid, () => resolve());
    });
    context.commit('TASKSetInprogress', {
        id: task.info.id,
        inProgress: false
    });
}
export async function startTask(context: Context, id: string) {
    const task = context.state.videoQueue.find(task => task.info.id === id);
    if (!task) {
        return;
    }
    context.commit('TASKSetStatus', {
        id: task.info.id,
        status: 'ready'
    });
    context.commit('TASKSetIsStopped', {
        id: task.info.id,
        isStopped: false
    });
    context.commit('TASKSetInprogress', {
        id: task.info.id,
        inProgress: false
    });
    // context.dispatch('performVideoTask', task);
}
export async function doNextTask(context: Context) {
    if(context.state.videoQueue.filter(x => x.inProgress).length === 1){
        return;
    }
    if (context.state.isWorking) {
        return;
    }
    const task = await getNextReadyTask(context);
    if (!task) {
        return;
    }
    context.dispatch('performVideoTask', task);
}
export async function removeAllTasksWithStatus(context:Context, status: VideoTaskStatus){
    const videos = context.state.videoQueue.filter(x => x.status === status);
    console.log(videos);
    videos.forEach(task => {
        if(_ProcessBank[task.info.id]){
            terminate(_ProcessBank[task.info.id]);
        }
        removeTempFolder(task);
        context.dispatch('removeTask', task);
    });
}
export async function removeTask(context:Context, task:VideoTask){
    removeTempFolder(task);
    context.commit('clearTask', task.info.id);
}
export async function stopAllTasks(context:Context){
    const tasks = context.state.videoQueue.filter(task => !task.isStopped && task.status !== VideoTaskStatus.done && task.status !== VideoTaskStatus.error);
    tasks.forEach(task => context.dispatch('stopTask', task.info.id));
}
export async function startAllTasks(context:Context){
    const tasks = context.state.videoQueue.filter(task => task.isStopped);
    tasks.forEach(task => context.dispatch('startTask', task.info.id));
}

/**
 *
 * @param {MediaManagerContext} context
 */
export async function init(context: Context) {
    console.log('init');
    setInterval(() => context.dispatch('doNextTask'), 1200);
    setInterval(() =>{
        Object.keys(_ProcessBank).forEach(taskId => {
            const video = context.state.videoQueue.find(x => x.info.id === taskId);
            if(!video){
                return;
            } 
            if(video.inProgress){
                return;
            }
            const process = _ProcessBank[taskId];
            if(!process){
                return;
            }
            terminate(process.pid);
        });
    }, 600);
}
