import {VideoInfo} from '../types/Video/VideoInfo';
import { VideoFormat } from '../types/Video/VideoFormat';

export interface IndexState{
    video: VideoInfo | null,
    link: string,
    folder: string,
    formats: VideoFormat[],
    chosenFormat: VideoFormat
}

export interface IndexContext{
    commit(fn:string, payload?:any): any,
    dispatch(fn:string, payload?:any): any,
    state: IndexState
}
