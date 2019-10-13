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
    if (context.state.isGettingVideoInformation) {
        throw new Error('Already fetching a video');
    }
    context.commit('SET_IS_GETTING_VIDEO_INFORMATION_STATUS', true);
    let info;
    try {
        info = await context.dispatch('getVideoFormatInfo', { url, format: 'bestvideo+bestaudio' });
    } catch (error) {}
    if (!info) {
        try {
            info = await context.dispatch('getVideoFormatInfo', { url });
        } catch (error) {}
    }
    context.commit('SET_IS_GETTING_VIDEO_INFORMATION_STATUS', false);
    if (!info) {
        throw new Error("Video couldn't be loaded");
    }
    info = JSON.parse(info);
    info.duration_formatted = formatDuration(info.duration) || 'Unknown';
    context.commit('setCurrentVideo', info);
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
    try {
        context.commit('SET_IS_WORKING', true);
        const task = context.state.videoQueue.find((task) => task.status === 'ready');
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

        context.commit('SetVideoTaskStatus', { id: task.id, status: 'done' });
    } catch (error) {
    } finally {
    }
    // // Combining section
    // if (tempFileNames.length > 1) {
    //     this.DownloadSection.status = 'Combining media...';
    //     const tempFilename = `${tempFileNames.join('')}.${chosenExt}`;
    //     const tempFilePath = path.join(directory, tempFolder, tempFilename);
    //     TrackProgress({
    //         file: tempFilePath,
    //         size: 0,
    //         totalSize: infos.reduce((acc, curr) => acc + curr.filesize, 0),
    //         listener: ({ CurrentProgress }) => {
    //             this.DownloadSection.progress = CurrentProgress;
    //         }
    //     });
    //     const { error } = await handlePromise(
    //         combine({
    //             cwd: path.join(directory, tempFolder),
    //             inputs: tempFileNames,
    //             output: tempFilename
    //         })
    //     );
    //     UnTrackProgress({ file: tempFilePath });
    //     if (error) {
    //         HandleFailed({
    //             message: error,
    //             tempPath: path.join(directory, tempFolder)
    //         });
    //         console.error(error);
    //         return;
    //     }
    //     tempFileNames.push(tempFilename);
    // }
    // const last = getLast(tempFileNames);
    // // Convert section
    // if (path.extname(last) !== `.${chosenExt}`) {
    //     this.DownloadSection.status = 'Converting media...';
    //     const tempFilename = `${last}.${chosenExt}`;
    //     const tempFilePath = path.join(directory, tempFolder, tempFilename);
    //     TrackProgress({
    //         file: tempFilePath,
    //         size: 0,
    //         totalSize: infos.reduce((acc, curr) => acc + curr.filesize, 0),
    //         listener: ({ CurrentProgress }) => {
    //             this.DownloadSection.progress = CurrentProgress;
    //         }
    //     });
    //     const { error } = await handlePromise(
    //         convert({
    //             cwd: path.join(directory, tempFolder),
    //             input: last,
    //             output: tempFilename
    //         })
    //     );
    //     UnTrackProgress({
    //         file: tempFilePath
    //     });
    //     if (error) {
    //         HandleFailed({
    //             message: error,
    //             tempPath: path.join(directory, tempFolder)
    //         });
    //         console.error(error);
    //         return;
    //     }
    // }
    // fs.moveSync(path.join(directory, tempFolder, getLast(tempFileNames)), path.join(directory, filename));
    // fs.remove(path.join(directory, tempFolder));
    // this.DownloadSection.progress = 1;
    // this.DownloadSection.isFinished = true;
}
