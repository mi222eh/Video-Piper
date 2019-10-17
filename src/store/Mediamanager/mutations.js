import './doc/MediaManagerDoc';
/**
 *
 * @param {MediaManagerState} state
 * @param {VideoInfo} video
 */
export function setCurrentVideo (state, video) {
    state.video = video;
}
/**
 *
 * @param {MediaManagerState} state
 * @param {Boolean} status
 */
export function SET_IS_GETTING_VIDEO_INFORMATION_STATUS (state, status) {
    state.isGettingVideoInformation = status;
}
/**
 *
 * @param {MediaManagerState} state
 * @param {Boolean} status
 */
export function SET_IS_WORKING (state, status) {
    state.isWorking = status;
}
/**
 *
 * @param {MediaManagerState} state
 * @param {VideoTask} videoTask
 */
export function addVideoTaskToQueue (state, videoTask) {
    state.videoQueue.push(videoTask);
}

/**
 *
 * @param {MediaManagerState} state
 * @param {{id:String, percentage:Number}} payload
 */
export function setPercentageOnTask (state, payload) {
    const videoTask = state.videoQueue.find((task) => task.id === payload.id);
    videoTask.percentage = payload.percentage;
}
/**
 *
 * @param {MediaManagerState} state
 * @param {Number} id
 */
export function clearTask (state, id) {
    state.videoQueue = state.videoQueue.filter((task) => task.id !== id);
}
/**
 *
 * @param {MediaManagerState} state
 * @param {Number} id
 */
export function clearFinishedTasks (state) {
    state.videoQueue = state.videoQueue.filter((task) => task.status !== 'done');
}

/**
 *
 * @param {MediaManagerState} state
 * @param {{id:Number, status:String}} payload
 */
export function SetVideoTaskStatus (state, payload) {
    const videoTask = state.videoQueue.find((task) => task.id === payload.id);
    videoTask.status = payload.status;
}
