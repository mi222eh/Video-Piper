import { State } from './mediaManagerTypes';

export function getTaskQueue(state: State) {
    return state.videoQueue;
}
export function getActiveQueueCount(state: State) {
    return state.videoQueue.filter(x => !x.isFinished).length;
}
export function getNumberOfOngoingTasks(state:State){
    return state.videoQueue.filter(x => x.inProgress).length
}
