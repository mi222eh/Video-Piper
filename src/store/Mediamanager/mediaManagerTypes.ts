import { VideoInfo } from '../types/Video/VideoInfo';
import { VideoFormat } from '../types/Video/VideoFormat';
import { ChildProcess } from 'child_process';

export interface State{
    isWorking: boolean,
    videoQueue: VideoTask[]
}
export interface Context{
    commit(mutator:string, payload?:any): void,
    dispatch(fn:string, payload?:any): any,
    state: State
}

export enum VideoTaskStatus{
    ready='ready',
    stopped = 'stopped',
    preparing = 'preparing',
    error = 'error',
    downloading='downloading',
    combining = 'combining',
    converting = 'converting',
    done = 'done'
}

export interface VideoTask{
    info: VideoTaskInfo,
    process: ChildProcess | null,
    percentage: number,
    status: VideoTaskStatus,
    statusText: string,
    inProgress:boolean,
    isStopped: boolean,
    isFinished: boolean
}

export interface VideoTaskInfo{
    chosenFormat: VideoFormat,
    videoInfo: VideoInfo,
    folder: string,
    URL: string,
    tempFileNames: string[],
    tempFolder: string,
    id: string,
    actionHistory: string[],
    groupId: string,
    groupName: string
}
export interface ProcessBank{
    [taskId:string]: ChildProcess | null | undefined
}
