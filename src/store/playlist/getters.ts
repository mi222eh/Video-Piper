import { PlaylistState } from "./playlistTypes";


export function GetIsBusy (state: PlaylistState) {
    return state.isBusy;
}
export function GetNumberOfFinished(state:PlaylistState){
    return state.finishedVideos;
}
export function GetTotalNumberOfVideos(state:PlaylistState){
    return state.totalVideos;
}
