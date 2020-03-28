// Generated by https://quicktype.io

export interface VideoPlaylist {
    entries:              Entry[];
    uploader:             string;
    _type:                string;
    extractor:            string;
    id:                   string;
    uploader_url:         string;
    title:                string;
    webpage_url:          string;
    extractor_key:        string;
    webpage_url_basename: string;
    uploader_id:          string;
}

export interface Entry {
    title:  string;
    url:    string;
    ie_key: String;
    _type:  String;
    id?:     string;
}