import './doc/MediaManagerDoc';
import { State, VideoTask, VideoTaskStatus } from './mediaManagerTypes';
import { VideoInfo } from '../types/Video/VideoInfo';
import merge from 'merge';
import state from './state';
import { ChildProcess } from 'child_process';

function getTask (state:State, id:string){
    const task = state.videoQueue.find(task => task.info.id === id);
    if(!task){
        throw new Error("Task not found");
    }
    return task;
}

export function SET_IS_WORKING(state: State, status: boolean) {
    state.isWorking = status;
}

export function addVideoTaskToQueue(state: State, videoTask: VideoTask) {
    state.videoQueue.push(videoTask);
}
export function clearTask(state: State, id: string) {
    state.videoQueue = state.videoQueue.filter(task => !task.inProgress && task.info.id !== id);
}
/**
 *
 * @param {MediaManagerState} state
 * @param {Number} id
 */
export function clearFinishedTasks(state: State) {
    state.videoQueue = state.videoQueue.filter(task => task.status !== 'done');
}

export function SetVideoTaskStatus(
    state: State,
    payload: {
        id: string;
        status:VideoTaskStatus;
    }
) {
    const videoTask = state.videoQueue.find(
        task => task.info.id === payload.id
    );
    if (!videoTask) {
        return;
    }
    videoTask.status = payload.status;
}
export function TaskUpdateInformation(state: State, payload:{id:string, task: object}) {
    const taskIndex = state.videoQueue.findIndex(vid => vid.info.id === payload.id);
    const task = state.videoQueue[taskIndex];
    if(!task){
        return;
    }
    const updatedTask = Object.assign(task, payload.task);
    state.videoQueue.splice(taskIndex, 1, updatedTask);
}

// TASK FUNCTIONS
export function TASKAddTempFilename(state:State, payload:{id:string, filename:string}) {
    try{
        const task = getTask(state, payload.id);
        task.info.tempFileNames.push(payload.filename);
    }
    catch(err){
        console.error(err);
    }
}

export function TASKSetPercentage(state:State, payload:{id:string, percentage:number}){
    const task = state.videoQueue.find(task => task.info.id === payload.id);
    if(!task){
        return;
    }
    task.percentage = payload.percentage;
}
export function TASKSetInprogress(state:State, payload:{id:string, inProgress:boolean}){
    try{
        const task = getTask(state, payload.id);
        task.inProgress = payload.inProgress;
    }
    catch(err){
        console.error(err);
    }
}
export function TASKSetIsStopped(state:State, payload:{id:string, isStopped:boolean}){
    try{
        const task = getTask(state, payload.id);
        task.isStopped = payload.isStopped;
    }
    catch(err){
        console.error(err);
    }
}
export function TASKSetIsFinished(state:State, payload:{id:string, isFInished:boolean}){
    try{
        const task = getTask(state, payload.id);
        task.isFinished = payload.isFInished;
    }
    catch(err){
        console.error(err);
    }
}
export function TASKSetStatus(state:State, payload:{id:string, status:VideoTaskStatus}){
    try{
        const task = getTask(state, payload.id);
        task.status = payload.status;
    }
    catch(err){
        console.error(err);
    }
}
