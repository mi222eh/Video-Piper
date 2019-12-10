import { VideoPlaylist } from "../types/VideoPlaylist";
import { VideoInfo } from "../types/Video/VideoInfo";
import { PlaylistTask, PlaylistContext, PlaylistMutators } from "./playlistTypes";
import { VideoTaskInfo } from "../Mediamanager/mediaManagerTypes";
import { VideoFormat } from "../types/Video/VideoFormat";

export async function addPlaylistItems(context: PlaylistContext, playlist: PlaylistTask) {
    if(context.state.isBusy){
        throw new Error("Busy getting videos");
    }
    context.commit(PlaylistMutators.SET_IS_BUSY, true);
    context.commit(PlaylistMutators.SET_NUBMER_OF_TOTAL_VIDEOS, playlist.playlist.entries.length);
    let numberOfFinished = 0;
    for (const entry of playlist.playlist.entries) {
        try {
            const info: VideoInfo = await context.dispatch('mediamanager/getVideoFormatInfo', {
                url: entry.url,
                format: playlist.format
            }, { root: true });

            const format: VideoFormat = {
                abr: info.abr,
                acodec: info.acodec,
                asr: info.asr,
                ext: playlist.ext || info.ext,
                format: playlist.format,
                filesize: info.filesize,
                format_id: playlist.format,
                format_note: 'Playlist',
            }
            const videotask: VideoTaskInfo = {
                URL: entry.url,
                actionHistory: [],
                chosenFormat: format,
                
                folder: playlist.folder,
                groupId: entry.ie_key + playlist.id,
                groupName: playlist.title,
                id:
                    info.extractor_key +
                    info.id +
                    info.format_id,
                tempFileNames: [],
                tempFolder: '',
                videoInfo: info
            }
            context.dispatch('mediamanager/addTaskToQueue', videotask, { root: true });
            numberOfFinished++;
            context.commit(PlaylistMutators.SET_FINISHED_VIDEOS, numberOfFinished);
        }
        catch (error) {
            console.error(error);
        }
    }

    context.commit(PlaylistMutators.SET_IS_BUSY, false);
}
