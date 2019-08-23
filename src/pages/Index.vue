<template>
  <div class="q-pa-md">
    <div class="q-gutter-md">
      <q-card class="get_info_card theme_background" primary>
        <!--DIRECTORY INPUT-->
        <q-item clickable v-ripple @click="ChooseDirectory">
          <q-item-section avatar top>
            <q-avatar icon="folder" color="primary" text-color="white" />
          </q-item-section>
          <q-item-section>
            <q-item-label>Output Directory</q-item-label>
            <q-item-label caption>{{Directory}}</q-item-label>
          </q-item-section>
        </q-item>
        <!--URL INPUT-->
        <q-input ref="URLInput" v-model="VideoUrl" label="Video URL" />
        <!--GET INFO BUTTON-->
        <q-btn
          class="full-width get_info_button"
          :loading="IsGettingVideoInformation"
          color="primary"
          label="Get Info"
          @click="GetVideoInfo"
        />
      </q-card>
    </div>

    <!--VIDEO INFORMATION CARD-->
    <q-card v-if="InfoSection.Show">
      <!--THUMBNAIL-->
      <q-img :src="InfoSection.data.thumbnail" spinner-color="white" basic class="video-thumbnail">
        <div class="absolute-bottom text-subtitle2 text-center">{{InfoSection.data.title}}</div>
      </q-img>
      <q-card-section>
        <!--BASIC INFORMATION-->
        <q-card>
          <q-card-section
            v-for="(Info, index) in InfoSection.InfoToDisplay"
            :key="`${index}-${Info.field}`"
          >
            <q-item-label>{{Info.label}}</q-item-label>
            <q-item-label caption lines="2">{{InfoSection.data[Info.field]}}</q-item-label>
          </q-card-section>
        </q-card>
        <q-card>
          <q-item clickable v-ripple @click="ShowFormats = true">
            <q-item-section>
              <q-item-label>Format{{ChosenFormat.height ? ` - ${ChosenFormat.height}P` : ''}}</q-item-label>
              <q-item-label
                caption
              >{{ChosenFormat.format}}{{ChosenFormat.filesize ? ' at ' + (Math.round(ChosenFormat.filesize / 1000000)) + 'MB': ''}}</q-item-label>
            </q-item-section>

            <q-item-section side top>
              <q-item-label caption>{{ChosenFormat.ext}}</q-item-label>
            </q-item-section>
          </q-item>
        </q-card>
      </q-card-section>

      <!--DOWNLOAD BUTTON-->
      <q-card-section>
        <q-btn color="secondary" label="Download" @click="DownloadVideo" />
      </q-card-section>
    </q-card>

    <!--CHOOSE FORMAT DIALOG-->
    <q-dialog v-model="ShowFormats" transition-show="scale" transition-hide="scale">
      <q-card style="width: 300px">
        <q-card-section class="row items-center">
          <div class="text-h6">Formats</div>
          <q-space />
          <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-separator />
        <q-card-section style="max-height: 50vh" class="scroll">
          <q-list bordered class="rounded-borders">
            <q-expansion-item
              v-for="(category, index) in InfoSection.formats"
              :key="`${index}-Category`"
              expand-separator
              :label="`${category.label}`"
              caption
            >
              <q-list v-for="(format, index) in category.list" :key="`${index}-format`">
                <q-item
                  clickable
                  v-close-popup
                  v-ripple
                  @click="ChosenFormat = format"
                  :active="ChosenFormat.format_id === format.format_id"
                >
                  <q-item-section>
                    <q-item-label>Format{{format.height ? ` - ${format.height}P` : ''}}</q-item-label>
                    <q-item-label
                      caption
                    >{{format.format}}{{format.filesize ? ' at ' + (Math.round(format.filesize / 1000000)) + 'MB': ''}}</q-item-label>
                  </q-item-section>

                  <q-item-section side top>
                    <q-item-label caption>{{format.ext}}</q-item-label>
                  </q-item-section>
                </q-item>
              </q-list>
            </q-expansion-item>
          </q-list>
        </q-card-section>
      </q-card>
    </q-dialog>

    <!--DOWNLOAD DIALOG-->
    <q-dialog
      v-model="ShowDownloadDialog"
      persistent
      transition-show="scale"
      transition-hide="scale"
    >
      <q-card style="width: 300px">
        <!--TITLE AND PROGRESS BAR-->
        <q-card-section>
          <div class="text-h6">Download in progress</div>
        </q-card-section>
        <q-separator />

        <q-linear-progress
          v-if="DownloadSection.progress === -1"
          query
          style="height: 15px"
          :value="this.DownloadSection.progress"
        />

        <q-linear-progress
          v-else-if="DownloadSection.progress === 404 "
          indeterminate
          stripe
          style="height: 15px"
          :value="this.DownloadSection.progress"
        />

        <q-linear-progress v-else stripe style="height: 15px" :value="DownloadSection.progress" />

        <q-separator />

        <!--STATUS-->
        <q-card-section>
          <div v-if="DownloadSection.isFinished">
            <div v-if="DownloadSection.failed">Error: {{DownloadSection.errorMessage}}</div>
            <div v-else style="color:green">Completed</div>
          </div>
          <div v-else>{{DownloadSection.status}}</div>
        </q-card-section>

        <!--DIALOG CLOSE BUTTON-->
        <q-card-actions align="right" class="bg-white text-teal">
          <q-btn flat v-if="DownloadSection.isFinished" label="OK" v-close-popup />
          <q-btn flat v-else label="Cancel" v-close-popup @click="Abort()" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import path from 'path';
import sanitizeFilename from 'sanitize-filename';
import { ipcRenderer } from 'electron';
import fs from 'fs-extra';

const getLast = arr => arr[arr.length - 1];

const Factory = {
    Format: function ({
        ext,
        filesize,
        id,
        format_id: formatId,
        height,
        format,
        tbr,
        abr,
        quality,
        protocol,
        player_url: playerUrl,
        vcodec,
        acodec,
        fps,
        title
    }) {
        return {
            ext,
            filesize,
            id,
            formatId,
            height: height || '',
            format,
            tbr,
            abr,
            quality,
            protocol,
            playerUrl,
            vcodec,
            title
        };
    }
};
const convert = ({ cwd, input, output }) => {
    return new Promise((resolve, reject) => {
        ipcRenderer.once('client/ffmpeg/convert', (event, args) => {
            const { error, result } = args;
            if (error) {
                reject(error);
            } else {
                resolve({
                    result,
                    args: {
                        cwd,
                        input,
                        output
                    }
                });
            }
        });
        ipcRenderer.send('ffmpeg/convert', {
            cwd,
            input,
            output
        });
    });
};
const combine = ({ cwd, inputs = [], output }) => {
    return new Promise((resolve, reject) => {
        ipcRenderer.once('client/ffmpeg/combine', (event, args) => {
            const { error, result } = args;
            if (error) {
                reject(error);
            } else {
                resolve({
                    result,
                    args: {
                        cwd,
                        inputs,
                        output
                    }
                });
            }
        });
        ipcRenderer.send('ffmpeg/combine', {
            cwd,
            inputs,
            output
        });
    });
};
const RemoveIllegalFilenameCharacters = function (filename) {
    filename = sanitizeFilename(filename);
    filename = filename.replace('  ', ' ');
    return filename;
};
const FormatDuration = function (ms) {
    if (ms < 0) ms = -ms;
    const time = {
        day: Math.floor(ms / 86400000),
        hour: Math.floor(ms / 3600000) % 24,
        minute: Math.floor(ms / 60000) % 60,
        second: Math.floor(ms / 1000) % 60,
        millisecond: Math.floor(ms) % 1000
    };
    return Object.entries(time)
        .filter(val => val[1] !== 0)
        .map(([key, val]) => `${val} ${key}${val !== 1 ? 's' : ''}`)
        .join(', ');
};
const asyncEvery = async function (array, callback) {
    let ok = true;
    for (let index = 0; index < array.length; index++) {
        ok = await callback(array[index], index, array);
        if (!ok) {
            index = array.length + 1;
        }
    }
    return ok;
};
const TrackProgress = function ({ file, size = 0, totalSize, listener }) {
    fs.watchFile(
        file,
        {
            interval: 200
        },
        (current, previous) => {
            if (typeof totalSize === 'number' && totalSize > 0) {
                let percentage = (current.size + size) / totalSize;
                console.log(percentage);

                listener({ CurrentProgress: percentage });
                return;
            }
            listener({ CurrentProgress: 404 });
        }
    );
};
const UnTrackProgress = function ({ file }) {
    try {
        fs.unwatchFile(file);
    } catch {}
};
const downloadMedia = ({ url, cwd, filename, format }) => {
    return new Promise((resolve, reject) => {
        ipcRenderer.once('client/youtubedl/start_video_stream', (event, args) => {
            const { error, result } = args;
            if (error) {
                reject(error);
            } else {
                resolve(result);
            }
        });
        ipcRenderer.send('youtubedl/start_video_stream', {
            cwd,
            url,
            filename,
            format
        });
    });
};
const downloadInfo = ({ url, format, cwd }) => {
    return new Promise((resolve, reject) => {
        ipcRenderer.once(
            'client/youtubedl/get_info',
            (event, { error, result }) => {
                if (error) {
                    reject(error);
                } else {
                    resolve(result);
                }
            }
        );
        ipcRenderer.send('youtubedl/get_info', {
            cwd,
            url,
            format
        });
    });
};

const handlePromise = function (prom) {
    return new Promise((resolve, reject) => {
        prom
            .then(result => {
                resolve({
                    result
                });
            })
            .catch(error => {
                resolve({
                    error
                });
            });
    });
};
const removeFolder = function ({ tempPath }) {
    fs.remove(tempPath);
};

export default {
    name: 'PageIndex',
    created () {},
    methods: {
        ChooseDirectory: async function () {
            this.Directory = this.$q.electron.remote.dialog.showOpenDialog({
                properties: ['openDirectory']
            })[0];
        },
        GetVideoInfo: async function () {
            if (!this.VideoUrl) {
                this.$q.notify('Input the video url');
                return;
            }
            // Set variables
            this.CurrentVideoUrl = this.VideoUrl;
            this.IsGettingVideoInformation = true;
            this.InfoSection.Show = false;

            // Get video info
            let { error, result } = await handlePromise(
                downloadInfo({
                    url: this.CurrentVideoUrl,
                    format: 'bestvideo+bestaudio',
                    cwd: this.Directory
                })
            );
            if (error) {
                // Try again
                ({ error, result } = await handlePromise(
                    downloadInfo({
                        url: this.CurrentVideoUrl,
                        format: '',
                        cwd: this.Directory
                    })
                ));
                if (error) {
                    // Error all the way
                    console.error(error);
                    this.$q.notify('Could not get info');
                    this.IsGettingVideoInformation = false;
                    return;
                }
            }
            let info = JSON.parse(result);
            // Set variables
            this.IsGettingVideoInformation = false;
            this.InfoSection.data = info;
            this.InfoSection.formats.audio.list = [];
            this.InfoSection.formats.video.list = [];
            this.InfoSection.formats.audioAndVideo.list = [];
            this.InfoSection.formats.custom.list = [];

            // Get different formats
            const audioOnly = info.formats.filter(format => format.vcodec === 'none');
            const videoOnly = info.formats.filter(format => format.acodec === 'none');
            const audioAndVideo = info.formats.filter(
                format => format.vcodec !== 'none' && format.acodec !== 'none'
            );
            const other = info.formats.filter(
                format =>
                    audioOnly.some(x => format.format_id !== x.format_id) &&
          videoOnly.some(x => format.format_id !== x.format_id) &&
          audioAndVideo.some(x => format.format_id !== x.format_id)
            );

            // Set formats
            this.InfoSection.formats.audio.list = audioOnly;
            this.InfoSection.formats.video.list = videoOnly;
            this.InfoSection.formats.audioAndVideo.list = audioAndVideo;
            this.InfoSection.formats.other.list = other;

            // Split format id
            const formatIds = info.format_id.split('+');
            if (formatIds.length > 1) {
                const bestVideo = info.formats.find(
                    format =>
                        formatIds.some(formatId => formatId === format.format_id) &&
            format.vcodec !== 'none'
                );
                const bestAudio = info.formats.find(
                    format =>
                        formatIds.some(formatId => formatId === format.format_id) &&
            format.acodec !== 'none'
                );

                const best1080pVideo = info.formats
                    .filter(format => format.width === 1920)
                    .reduce((accumulator, currentValue, currentIndex, array) => {
                        console.log({
                            accumulator,
                            currentValue
                        });
                        if (accumulator === false) {
                            return currentValue;
                        }
                        if (currentValue.filesize > accumulator.filesize) {
                            return currentValue;
                        }
                        return accumulator;
                    }, false);

                const bestAudioAndVideoPreset = {
                    abr: bestAudio.abr,
                    acodec: bestAudio.acodec,
                    ext: 'mp4',
                    filesize: bestAudio.filesize + bestVideo.filesize,
                    format: 'Best video and audio',
                    format_id: info.format_id,
                    format_note: 'Best video and audio',
                    quality: -1,
                    height: bestVideo.height,
                    vcodec: bestVideo.vcodec
                };
                let best1080VideoPreset = {};
                const bestAudioPreset = {
                    abr: bestAudio.abr,
                    acodec: bestAudio.acodec,
                    ext: 'mp3',
                    filesize: bestAudio.filesize,
                    format: 'Best audio',
                    format_id: bestAudio.format_id,
                    format_note: 'Best audio only',
                    quality: -1
                };
                const bestVideoPreset = {
                    ext: 'mp4',
                    filesize: bestVideo.filesize,
                    format: 'Best video',
                    format_id: bestVideo.format_id,
                    format_note: 'Best video only',
                    quality: -1,
                    height: bestVideo.height,
                    vcodec: bestVideo.vcodec
                };
                if (best1080pVideo) {
                    best1080VideoPreset = {
                        abr: bestAudio.abr,
                        ext: 'mp4',
                        filesize: best1080pVideo.filesize + bestAudio.filesize,
                        format: 'Best 1080P video and audio',
                        format_id: best1080pVideo.format_id + '+' + bestAudio.format_id,
                        format_note: 'Best 1080P video and audio',
                        quality: -1,
                        height: best1080pVideo.height,
                        vcodec: best1080pVideo.vcodec
                    };
                }
                this.InfoSection.formats.custom.list.push(bestAudioAndVideoPreset);
                if (best1080pVideo) {
                    this.InfoSection.formats.custom.list.push(best1080VideoPreset);
                }
                this.InfoSection.formats.custom.list.push(bestAudioPreset);
                this.InfoSection.formats.custom.list.push(bestVideoPreset);

                info.formats.unshift(bestAudioAndVideoPreset);
            }

            this.ChosenFormat = info.formats.find(
                format => format.format_id === info.format_id
            );
            info.duration_formatted = FormatDuration(info.duration);
            this.InfoSection.Show = true;
        },
        HandleFailed: function ({ message, tempPath }) {
            console.log({ message, tempPath });

            if (tempPath) {
                removeFolder({ tempPath });
            }

            this.DownloadSection.failed = true;
            this.DownloadSection.isFinished = true;
            this.DownloadSection.errorMessage = message;
            this.DownloadSection.progress = 1;
        },
        DownloadVideo: async function () {
            if (!this.Directory) {
                this.$q.notify('Please choose a directory');
                return;
            }
            if (!this.ChosenFormat) {
                this.$q.notify('Please choose a format');
                return;
            }

            const HandleFailed = this.HandleFailed;

            this.DownloadSection.status = 'Preparing...';
            this.ShowDownloadDialog = true;
            this.DownloadSection.isFinished = false;
            this.DownloadSection.progress = -1;
            this.DownloadSection.failed = false;
            const url = this.CurrentVideoUrl;
            const directory = this.Directory;
            const chosenExt = this.ChosenFormat.ext;
            const filename = RemoveIllegalFilenameCharacters(
                `${this.InfoSection.data.fulltitle}${
                    this.ChosenFormat.height ? ` - ${this.ChosenFormat.height}P` : ''
                }.${this.ChosenFormat.ext}`
            );

            // Get info section
            this.DownloadSection.status = 'Getting information...';
            let formatIds = Factory.Format(this.ChosenFormat).formatId.split('+');
            let infos = [];
            while (formatIds.length > 0) {
                const { error, result } = await handlePromise(
                    downloadInfo({ url, format: formatIds.pop(), cwd: directory })
                );
                if (error) {
                    this.HandleFailed({ error });
                    return;
                }
                infos.push(Factory.Format(JSON.parse(result)));
            }
            console.log(infos);

            const totalSize = infos.reduce((acc, curr) => acc + curr.filesize, 0);
            // Create folder
            const tempFolder = infos[0].id;
            await fs.mkdirp(path.join(directory, tempFolder));

            // Download media(s)
            let tempFileNames = [];
            let totalDownloaded = 0;
            this.DownloadSection.status = 'Piping media...';
            let isOk = await asyncEvery(infos, async info => {
                let tempName = `${info.formatId}.${info.ext}`;
                const currentTempFilePath = path.join(directory, tempFolder, tempName);
                tempFileNames.push(tempName);
                TrackProgress({
                    file: currentTempFilePath,
                    size: totalDownloaded,
                    totalSize: totalSize,
                    listener: ({ CurrentProgress }) => {
                        this.DownloadSection.progress = CurrentProgress;
                    }
                });
                const { error } = await handlePromise(
                    downloadMedia({
                        url,
                        cwd: path.join(directory, tempFolder),
                        filename: tempName,
                        format: info.formatId
                    })
                );
                UnTrackProgress({ file: currentTempFilePath });
                if (error) {
                    HandleFailed({
                        message: `Failed to download ${info.format}`,
                        tempPath: fs.join(directory, tempFolder)
                    });
                    return false;
                }
                totalDownloaded += info.filesize;
                return true;
            });
            if (!isOk) {
                return;
            }
            // Combining section
            if (tempFileNames.length > 1) {
                this.DownloadSection.status = 'Combining media...';
                const tempFilename = `${tempFileNames.join('')}.${chosenExt}`;
                const tempFilePath = path.join(directory, tempFolder, tempFilename);
                TrackProgress({
                    file: tempFilePath,
                    size: 0,
                    totalSize: infos.reduce((acc, curr) => acc + curr.filesize, 0),
                    listener: ({ CurrentProgress }) => {
                        this.DownloadSection.progress = CurrentProgress;
                    }
                });
                const { error } = await handlePromise(
                    combine({
                        cwd: path.join(directory, tempFolder),
                        inputs: tempFileNames,
                        output: tempFilename
                    })
                );
                UnTrackProgress({ file: tempFilePath });
                if (error) {
                    HandleFailed({
                        message: error,
                        tempPath: path.join(directory, tempFolder)
                    });
                    console.error(error);
                    return;
                }
                tempFileNames.push(tempFilename);
            }
            const last = getLast(tempFileNames);

            // Convert section
            if (path.extname(last) !== `.${chosenExt}`) {
                this.DownloadSection.status = 'Converting media...';
                const tempFilename = `${last}.${chosenExt}`;
                const tempFilePath = path.join(directory, tempFolder, tempFilename);
                TrackProgress({
                    file: tempFilePath,
                    size: 0,
                    totalSize: infos.reduce((acc, curr) => acc + curr.filesize, 0),
                    listener: ({ CurrentProgress }) => {
                        this.DownloadSection.progress = CurrentProgress;
                    }
                });
                const { error } = await handlePromise(
                    convert({
                        cwd: path.join(directory, tempFolder),
                        input: last,
                        output: tempFilename
                    })
                );
                UnTrackProgress({
                    file: tempFilePath
                });
                if (error) {
                    HandleFailed({
                        message: error,
                        tempPath: path.join(directory, tempFolder)
                    });
                    console.error(error);
                    return;
                }
            }

            fs.moveSync(
                path.join(directory, tempFolder, getLast(tempFileNames)),
                path.join(directory, filename)
            );
            fs.remove(path.join(directory, tempFolder));

            this.DownloadSection.progress = 1;
            this.DownloadSection.isFinished = true;
        },
        Abort: function () {
            ipcRenderer.send('abort');
        }
    },
    data () {
        return {
            VideoUrl: '',
            IsGettingVideoInformation: false,
            CurrentVideoUrl: '',
            ShowDownloadDialog: false,
            ChosenFormat: {},
            Directory: '',
            ShowFormats: false,
            InfoSection: {
                Show: false,
                data: {},
                formats: {
                    custom: {
                        label: 'Preset',
                        list: []
                    },
                    audioAndVideo: {
                        label: 'Audio and video',
                        list: []
                    },
                    video: {
                        label: 'Video only',
                        list: []
                    },
                    audio: {
                        label: 'Audio only',
                        list: []
                    },
                    other: {
                        label: 'Other',
                        list: []
                    }
                },
                InfoToDisplay: [
                    {
                        label: 'Title',
                        field: 'fulltitle'
                    },
                    {
                        label: 'Uploader',
                        field: 'uploader'
                    },
                    {
                        label: 'Duration',
                        field: 'duration_formatted'
                    },
                    {
                        label: 'Website',
                        field: 'extractor_key'
                    }
                ]
            },
            DownloadSection: {
                data: {},
                progress: 0,
                isFinished: false,
                status: '',
                failed: false,
                errorMessage: ''
            }
        };
    }
};
</script>

<style>
.get_info_button{
  margin-top: 10px;
}
.theme_background {
  background: linear-gradient();
}
.get_info_card {
  padding: 20px;
}
.video-thumbnail {
  max-width: 500px;
  display: block;
  margin: 0 auto;
}

.directory {
  display: inline;
}
</style>
