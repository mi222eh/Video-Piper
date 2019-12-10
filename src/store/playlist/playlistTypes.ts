import { VideoPlaylist } from "../types/VideoPlaylist";

export interface PlaylistContext{
    commit(mutator:string, payload?:any): void,
    dispatch(fn:string, payload?:any, options?:any): any,
    state: PlaylistState
}
export enum PlaylistGetters{
    GetIsBusy = "GetIsBusy",
    GetNumberOfFinished = "GetNumberOfFinished",
    GetTotalNumberOfVideos = "GetTotalNumberOfVideos"
}
export interface PlaylistState{
    isBusy: Boolean,
    totalVideos: Number,
    finishedVideos: Number
}
export enum PlaylistMutators{
    SET_FINISHED_VIDEOS = "SET_FINISHED_VIDEOS",
    SET_IS_BUSY = "SET_IS_BUSY",
    SET_NUBMER_OF_TOTAL_VIDEOS = "SET_NUBMER_OF_TOTAL_VIDEOS"
}
export interface PlaylistTask{
    format: string;
    playlist: VideoPlaylist;
    folder: string,
    title: string,
    id:string,
    ext:string
}
