import youtubeDl from '../modules/youtubedl';

export async function getVideoInfo ({ commit, state, dispatch }, url) {
    commit('SET_IS_GETTING_VIDEO_INFORMATION_STATUS', true);
    let info;
    try {
        info = await youtubeDl.getInfo({ url, format: 'bestvideo+bestaudio' });
    } catch (error) {}
    if (!info) {
        try {
            info = await youtubeDl.getInfo({ url, format: '' });
        } catch (error) {}
    }
    commit('SET_IS_GETTING_VIDEO_INFORMATION_STATUS', false);
    if (!info) {
        throw new Error("Video couldn't be loaded");
    }
    return info;
}
export async function getInfo (context) {}
