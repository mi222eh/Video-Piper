export interface GithubAsset {
    url:                  string;
    id:                   number;
    node_id:              string;
    name:                 string;
    label:                string;
    uploader:             Uploader;
    content_type:         ContentType;
    state:                State;
    size:                 number;
    download_count:       number;
    created_at:           Date;
    updated_at:           Date;
    browser_download_url: string;
}

export enum ContentType {
    ApplicationOctetStream = "application/octet-stream",
    ApplicationPGPSignature = "application/pgp-signature",
    ApplicationXTar = "application/x-tar",
}

export enum State {
    Uploaded = "uploaded",
}

export interface Uploader {
    login:               Login;
    id:                  number;
    node_id:             NodeID;
    avatar_url:          string;
    gravatar_id:         string;
    url:                 string;
    html_url:            string;
    followers_url:       string;
    following_url:       FollowingURL;
    gists_url:           GistsURL;
    starred_url:         StarredURL;
    subscriptions_url:   string;
    organizations_url:   string;
    repos_url:           string;
    events_url:          EventsURL;
    received_events_url: string;
    type:                Type;
    site_admin:          boolean;
}

export enum EventsURL {
    HTTPSAPIGithubCOMUsersDstftwEventsPrivacy = "https://api.github.com/users/dstftw/events{/privacy}",
}

export enum FollowingURL {
    HTTPSAPIGithubCOMUsersDstftwFollowingOtherUser = "https://api.github.com/users/dstftw/following{/other_user}",
}

export enum GistsURL {
    HTTPSAPIGithubCOMUsersDstftwGistsGistID = "https://api.github.com/users/dstftw/gists{/gist_id}",
}

export enum Login {
    Dstftw = "dstftw",
}

export enum NodeID {
    MDQ6VXNlcjE5MDg4OTg = "MDQ6VXNlcjE5MDg4OTg=",
}

export enum StarredURL {
    HTTPSAPIGithubCOMUsersDstftwStarredOwnerRepo = "https://api.github.com/users/dstftw/starred{/owner}{/repo}",
}

export enum Type {
    User = "User",
}
