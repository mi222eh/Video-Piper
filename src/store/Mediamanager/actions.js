import youtubeDl from '../modules/youtubedl';
import ffmpeg from '../modules/ffmpeg';
import { formatDuration, TrackProgress } from '../modules/helpers';
import sanitizeName from 'sanitize-filename';
import fs from 'fs-extra';
import path from 'path';
/**
  *
  * @param {MediaManagerContext} context
  * @param {String} url
  */
export async function getVideoInfo (context, url) {
    let info;
    try {
        info = await context.dispatch('getVideoFormatInfo', { url, format: 'bestvideo+bestaudio' });
    } catch (error) {}
    if (!info) {
        try {
            info = await context.dispatch('getVideoFormatInfo', { url });
        } catch (error) {}
    }
    if (!info) {
        throw new Error("Video couldn't be loaded");
    }
    info = JSON.parse(info);
    info.duration_formatted = formatDuration(info.duration) || 'Unknown';
    return info;
}
/**
  *
  * @param {MediaManagerContext} context
  * @param {{url:String, format:String}} opts
  */
export async function getVideoFormatInfo (context, opts) {
    return youtubeDl.getInfo({ url: opts.url, format: opts.format || '' });
}
/**
 * @param {MediaManagerContext} context
 * @param {VideoTask} videoTask
 */
export async function abort (context, id) {
    const task = context.state.videoQueue.find((x) => x.id === id);
    await youtubeDl.abort();
    await ffmpeg.abort();
    task.status = 'error';
}
/**
 * @param {MediaManagerContext} context
 * @param {VideoTask} videoTask
 */
export async function addVideoToTask (context, videoTask) {
    if (!videoTask.folder) {
        throw new Error('Please choose a folder');
    }
    if (videoTask.chosenFormat.length < 1) {
        throw new Error('Please choose a format');
    }
    videoTask.status = 'ready';
    videoTask.percentage = 0;
    context.commit('addVideoTaskToQueue', videoTask);
    context.dispatch('doNext');
}
/**
 * @param {MediaManagerContext} context
 */
export async function doNext (context) {
    if (context.state.isWorking) {
        return;
    }
    const task = context.state.videoQueue.find((task) => task.status === 'ready');
    if (!task) {
        return;
    }
    context.commit('SET_IS_WORKING', true);
    try {
        console.log(task);
        context.commit('SetVideoTaskStatus', { id: task.id, status: 'preparing' });
        /**
         * @type {VideoInfo[]}
         */
        let infos = [];
        for (const formatId of task.chosenFormat) {
            const info = await context.dispatch('getVideoFormatInfo', { url: task.URL, format: formatId });
            infos.push(JSON.parse(info));
        }
        const filename = sanitizeName(
            `${infos[0].fulltitle}${infos[0].height ? ` - ${infos[0].height}P` : ''}.${task.chosenExtension}`
        );
        console.log(filename);
        console.log('INFOS', infos);

        const totalSize = infos.reduce((acc, curr) => acc + curr.filesize, 0);

        // Create folder
        const tempFolder = path.join(task.folder, infos[0].id);
        await fs.mkdirp(tempFolder);
        // Download media(s)
        let tempFileNames = [];
        let totalDownloaded = 0;
        context.commit('SetVideoTaskStatus', { id: task.id, status: 'downloading' });
        for (const info of infos) {
            const tempFilename = `${info.format_id}.${info.ext}`;
            const tempPath = path.join(tempFolder, tempFilename);
            tempFileNames.push(tempFilename);
            const tracker = TrackProgress({
                file: tempPath,
                size: totalDownloaded,
                totalSize,
                listener: (progress) => {
                    context.commit('setPercentageOnTask', { id: task.id, percentage: progress });
                }
            });
            await youtubeDl.streamVideoIntoFile({
                cwd: tempFolder,
                filename: tempFilename,
                url: task.URL,
                format: info.format_id
            });
            tracker.close();
            totalDownloaded += info.filesize;
        }
        // Combining section
        if (tempFileNames.length > 1) {
            context.commit('SetVideoTaskStatus', { id: task.id, status: 'combining' });
            const tempFilename = `${tempFileNames.join('')}.${task.chosenExtension}`;
            const tempFilePath = path.join(tempFolder, tempFilename);
            const tracker = TrackProgress({
                file: tempFilePath,
                size: 0,
                totalSize,
                listener: (progress) => {
                    context.commit('setPercentageOnTask', { id: task.id, percentage: progress });
                }
            });
            await ffmpeg.combine({
                cwd: tempFolder,
                inputs: tempFileNames,
                output: tempFilename
            });

            tracker.close();
            tempFileNames.push(tempFilename);
        }
        // Convert section
        const last = tempFileNames[tempFileNames.length - 1];
        if (path.extname(last) !== `.${task.chosenExtension}`) {
            context.commit('SetVideoTaskStatus', { id: task.id, status: 'converting' });
            const tempFilename = `${last}.${task.chosenExtension}`;
            const tempFilePath = path.join(tempFolder, tempFilename);
            const tracker = TrackProgress({
                file: tempFilePath,
                size: 0,
                totalSize,
                listener: (progress) => {
                    context.commit('setPercentageOnTask', { id: task.id, percentage: progress });
                }
            });
            await ffmpeg.convert({
                cwd: tempFolder,
                input: last,
                output: tempFilename
            });
            tracker.close();
        }
        await fs.move(
            path.join(tempFolder, tempFileNames[tempFileNames.length - 1]),
            path.join(task.folder, filename),
            {
                overwrite: true
            }
        );
        await fs.remove(tempFolder);
        context.commit('setPercentageOnTask', { id: task.id, percentage: 1 });
        context.commit('SetVideoTaskStatus', { id: task.id, status: 'done' });
    } catch (error) {
        context.commit('SetVideoTaskStatus', { id: task.id, status: 'error' });
        console.error(error);
    } finally {
    }
    context.commit('SET_IS_WORKING', false);
    context.dispatch('doNext');
}
