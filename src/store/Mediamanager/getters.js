import './doc/MediaManagerDoc';
/**
 *
 * @param {MediaManagerState} state
 */
export function isGettingVideoInformation (state) {
    return state.isGettingVideoInformation;
}
/**
 *
 * @param {MediaManagerState} state
 */
export function getVideo (state) {
    return state.video;
}
/**
 *
 * @param {MediaManagerState} state
 */
export function getNextReadyTask (state) {
    return state.videoQueue.find((task) => task.status === 'ready');
}
/**
 *
 * @param {MediaManagerState} state
 */
export function getTaskQueue (state) {
    return state.videoQueue;
}
/**
 *
 * @param {MediaManagerState} state
 */
export function getActiveQueueCount (state) {
    return state.videoQueue.length;
}
