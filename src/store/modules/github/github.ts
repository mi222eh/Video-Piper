import { get } from 'https'
import { GithubAsset } from './github.types';


//@ts-ignore
import GitHub from 'github-api';

export async function getYoutubeDlLatestReleaseTag(): Promise<string>{
    const gh = new GitHub();
    const repo = await gh.getRepo('ytdl-org/youtube-dl');
    const latestRelease = await repo.getRelease('latest');
    return latestRelease.data.tag_name;
}