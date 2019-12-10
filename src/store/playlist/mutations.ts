import { PlaylistState } from "./state";

export function SET_FINISHED_VIDEOS (state:PlaylistState, finishedNumber) {
    state.finishedVideos = finishedNumber;
}

export function SET_IS_BUSY (state: PlaylistState, isBusy:Boolean) {
    state.isBusy = isBusy;
}

export function SET_NUBMER_OF_TOTAL_VIDEOS (state:PlaylistState, number:Number) {
    state.totalVideos = number;
}
