import { IndexState } from './indexTypes';
import { VideoInfo } from '../types/Video/VideoInfo';

export function setLink(state: IndexState, newLink: string) {
    state.link = newLink;
}
export function setVideo(state: IndexState, newVIdeoInfo: VideoInfo) {
    state.video = newVIdeoInfo;
}
export function setFolder(state: IndexState, newFolder: string) {
    state.folder = newFolder;
}
export function emptyVideo(state: IndexState) {
    state.video = null;
}
export function emptyLink(state: IndexState) {
    state.link = '';
}
