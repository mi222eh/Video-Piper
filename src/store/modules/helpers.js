import fs from 'fs-extra';
export function formatDuration (ms) {
    if (!ms) return;
    if (ms < 0) ms = -ms;
    const time = {
        day: Math.floor(ms / 86400000),
        hour: Math.floor(ms / 3600000) % 24,
        minute: Math.floor(ms / 60000) % 60,
        second: Math.floor(ms / 1000) % 60,
        millisecond: Math.floor(ms) % 1000
    };
    return Object.entries(time)
        .filter((val) => val[1] !== 0)
        .map(([ key, val ]) => `val key${val !== 1 ? 's' : ''}`)
        .join(', ');
}

/**
 *
 * @callback TrackOptionsCallback
 * @param {Number} percentage
 */

/**
 *
 * @typedef {Object} TrackOptions
 * @property {String} file
 * @property {Number} size
 * @property {Number} totalSize
 * @property {TrackOptionsCallback} listener
 */

/**
  *
  * @param {TrackOptions} param0
  */
export function TrackProgress ({ file, size = 0, totalSize, listener }) {
    const intId = setInterval(async () => {
        console.log('TRACKER');
        if (typeof totalSize === 'number' && totalSize > 0) {
            const fileInfo = await fs.stat(file);
            let percentage = (fileInfo.size + size) / totalSize;
            console.log(percentage);
            listener(percentage);
        } else {
            console.log(404);
            listener(404);
        }
    }, 400);

    return {
        close: () => clearInterval(intId)
    };
}
// /**
//  *
//  * @param {String} file
//  */
// export function UnTrackProgress (file) {
//     try {
//         fs.unwatchFile(file);
//     } catch (err) {
//         console.error(err);
//     }
// }
