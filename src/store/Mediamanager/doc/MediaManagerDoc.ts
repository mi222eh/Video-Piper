
/**
 * @typedef {Object} VideoInfo THE VIDEO INFO FROM THE YOUTUBE-DL
 * @property {String} duration_formatted
 * @property {String} _filename
 * @property {Number} abr
 * @property {String} acodec
 * @property {Number} age_limit
 * @property {Null} album
 * @property {Null} alt_title
 * @property {Null} annotations
 * @property {Null} artist
 * @property {Object} automatic_captions
 * @property {Number} average_rating
 * @property {String[]} categories
 * @property {String} channel_id
 * @property {String} channel_url
 * @property {Null} chapters
 * @property {Null} creator
 * @property {String} description
 * @property {Number} dislike_count
 * @property {String} display_id
 * @property {Number} duration
 * @property {Null} end_time
 * @property {Null} episode_number
 * @property {String} ext
 * @property {String} extractor
 * @property {String} extractor_key
 * @property {String} format
 * @property {String} format_id
 * @property {VideoFormat[]} formats
 * @property {Number} fps
 * @property {String} fulltitle
 * @property {Number} height
 * @property {String} id
 * @property {Null} is_live
 * @property {Null} license
 * @property {Number} like_count
 * @property {Null} playlist
 * @property {Null} playlist_index
 * @property {Null} release_date
 * @property {Null} release_year
 * @property {VideoFormat[]} requested_formats
 * @property {Null} requested_subtitles
 * @property {Null} resolution
 * @property {Null} season_number
 * @property {Null} series
 * @property {Null} start_time
 * @property {Null} stretched_ratio
 * @property {Object} subtitles
 * @property {Array} tags
 * @property {String} thumbnail
 * @property {{url:String, id:String}[]} thumbnails
 * @property {String} title
 * @property {Null} track
 * @property {String} upload_date
 * @property {String} uploader
 * @property {String} uploader_id
 * @property {String} uploader_url
 * @property {Null} vbr
 * @property {String} vcodec
 * @property {Number} view_count
 * @property {String} webpage_url
 * @property {String} webpage_url_basename
 * @property {Number} width
 */

/**
 * @typedef {Object} VideoFormat Video formats by youtube-dl
 * @property {String} acodec
 * @property {{http_chunk_size:String}} downloader_options
 * @property {String} ext
 * @property {Number} filesize
 * @property {String} format
 * @property {String} format_id
 * @property {String} format_note
 * @property {Number} fps
 * @property {Number} height
 * @property {Accept-Language:String, Accept-Encoding:String, User-Agent:String, Accept:String, Accept-Charset:String} http_headers
 * @property {String} player_url
 * @property {String} protocol
 * @property {Number} quality
 * @property {Number} tbr
 * @property {String} url
 * @property {String} vcodec
 * @property {Number} width
 */

/**
 * @typedef {Object} MediaManagerContext Context object of media managet
 * @property {Function} commit
 * @property {Function} dispatch
 * @property {MediaManagerState} state
 */
/**
 * @typedef {Object} MediaManagerState The state object
 * @property {Boolean} isWorking
 * @property {VideoTask[]} videoQueue
 */

/**
 * @typedef {Object} VideoTaskInfo
 * @property {VideoFormat} chosenFormat
 * @property {VideoInfo} videoInfo
 * @property {String} folder
 * @property {String} URL
 * @property {String[]} tempFileNames
 * @property {String} tempFolder
 * @property {String} id
 */

/**
 * @typedef {Object} VideoTask
 * @property {VideoTaskInfo} info
 * @property {ChildProcess} process
 * @property {Number} percentage
 * @property {"ready" | "stopped" | "preparing" | "error" | "downloading" | "combining" | "converting" | "done"} status
 * @property {String} statusText
 * @property {Boolean} inProgress
 * @property {Boolean} isStopped
 * @property {Boolean} isFinished
 */
