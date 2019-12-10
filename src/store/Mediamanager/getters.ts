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
export function getGroupedQueue(state:State){
    const groups = [...new Set(state.videoQueue.map(x => x.info.groupId))];
    return groups.map(groupId => {
        const videos = state.videoQueue.filter(x => x.info.groupId === groupId);
        return{
            videos,
            name: videos[0].info.groupName,
        }
    })
}