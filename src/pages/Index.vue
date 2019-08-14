<template>
  <div class="q-pa-md">
    <div class="q-gutter-md">
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
        :loading="IsGettingVideoInformation"
        color="primary"
        label="Get Info"
        @click="GetVideoInfo"
      />
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
import path, { join } from 'path';
import sanitizeFilename from 'sanitize-filename';
import { ipcRenderer } from 'electron';
import fs from 'fs-extra';

const Factory = {
	Format: class {
		height;
		format;
		filesize;
		format_id;
		ext;
		constructor({height, format, filesize, format_id, ext}){
			this.height = height;
			this.format = format;
			this.filesize = filesize;
			this.format_id = format_id;
			this.ext = ext;
		}
	}
};

const handleFailed = (error) => {
    console.error(error);
    this.DownloadSection.progress = 1;
    this.DownloadSection.failed = true;
    this.DownloadSection.errorMessage = error.toString();
    fs.removeSync(tempPath);
};
const convert = ({ totalSize }) => {
    const tempFilename = this.RemoveIllegalFilenameCharacters(getLast(tempFileNames) + '.' + chosenExt);
    this.TrackProgress({ filePath: path.join(tempPath, tempFilename), totalSize });
    return new Promise((resolve, reject) => {
        this.ConvertFile({
            cwd: tempPath,
            input: getLast(tempFileNames),
            output: tempFilename
        })
            .then((result) => {
                tempFileNames.push(tempFilename);
                resolve(result);
            })
            .catch(reject);
    });
};
const combine = ({ totalSize }) => {
    const tempFilename = this.RemoveIllegalFilenameCharacters(tempFileNames.reduce((acc, curr) => acc + curr, '') + '.' + chosenExt);
    this.TrackProgress({ filePath: path.join(tempPath, tempFilename), totalSize });
    return new Promise((resolve, reject) => {
        this.CombineVideoAndAudio({
            cwd: tempPath,
            inputs: tempFileNames,
            output: tempFilename
        })
            .then((result) => {
                tempFileNames.push(tempFilename);
                resolve(result);
            })
            .catch(reject);
    });
};
const downloadPart = (info) => {
    const tempFilename = this.RemoveIllegalFilenameCharacters(`${info.id}-${info.format_id}.${info.ext}`);
    tempFileNames.push(tempFilename);
    this.TrackProgress({ filePath: path.join(tempPath, tempFilename), totalSize: info.filesize || null });

    return this.StartVideoStream({
        url: url,
        cwd: tempPath,
        filename: tempFilename,
        format: info.format_id
    });
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
        GetVideoInfo: function () {
            if (!this.VideoUrl) {
                this.$q.notify('Input the video url');
                return;
            }
            // Set variables
            this.CurrentVideoUrl = this.VideoUrl;
            this.IsGettingVideoInformation = true;
            this.InfoSection.Show = false;

            let format = 'bestvideo+bestaudio';

            let info = await this.DownloadVideoInfo({ url: this.CurrentVideoUrl, format: isSecondTry ? '' : 'bestvideo+bestaudio', cwd: this.Directory });
            let info = JSON.parse(result);
            // Set variables
            this.IsGettingVideoInformation = false;
            this.InfoSection.data = info;
            this.InfoSection.formats.audio.list = [];
            this.InfoSection.formats.video.list = [];
            this.InfoSection.formats.audioAndVideo.list = [];
            this.InfoSection.formats.custom.list = [];

            // Split format id
            const formatIds = info.format_id.split('+');
            if (formatIds.length > 1) {
                const bestVideo = info.formats.find(format => formatIds.some(formatId => formatId === format.format_id) && format.vcodec !== 'none');
                const bestAudio = info.formats.find(format => formatIds.some(formatId => formatId === format.format_id) && format.acodec !== 'none');

                const audioOnly = info.formats.filter(format => format.vcodec === 'none');
                const videoOnly = info.formats.filter(format => format.acodec === 'none');
                const audioAndVideo = info.formats.filter(format => format.vcodec !== 'none' && format.acodec !== 'none');

                const best1080pVideo = info.formats.filter(format => format.width === 1920).reduce((accumulator, currentValue, currentIndex, array) => {
                    console.log({ accumulator, currentValue });
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

                this.InfoSection.formats.audio.list = audioOnly;
                this.InfoSection.formats.video.list = videoOnly;
                this.InfoSection.formats.audioAndVideo.list = audioAndVideo;

                this.InfoSection.formats.custom.list.push(bestAudioAndVideoPreset);
                if (best1080pVideo) {
                    this.InfoSection.formats.custom.list.push(best1080VideoPreset);
                }
                this.InfoSection.formats.custom.list.push(bestAudioPreset);
                this.InfoSection.formats.custom.list.push(bestVideoPreset);

                info.formats.unshift(bestAudioAndVideoPreset);
            }
            this.ChosenFormat = info.formats.find(format => format.format_id === info.format_id);
            this.InfoSection.Show = true;
            if (isSecondTry) {
                this.$q.notify('Error getting video information');
                this.IsGettingVideoInformation = false;
                return console.error(err);
            }
            this.DownloadVideoInfo(true);
        },
        DownloadVideo: function () {
            if (!this.Directory) {
                this.$q.notify('Please choose a directory');
                return;
            }
            if (!this.ChosenFormat) {
                this.$q.notify('Please choose a format');
                return;
            }

            this.ShowDownloadDialog = true;
            this.DownloadSection.status = 'Preparing';
            this.DownloadSection.isFinished = false;
            this.DownloadSection.progress = 0;
            this.DownloadSection.failed = false;

            const chosenExt = this.ChosenFormat.ext;
            const url = this.CurrentVideoUrl;
            const directory = this.Directory;
            const tempFolder = this.InfoSection.data.id;
            const tempPath = join(directory, tempFolder);
            const filename = this.RemoveIllegalFilenameCharacters(`${this.InfoSection.data.fulltitle}${this.ChosenFormat.height ? ` - ${this.ChosenFormat.height}P` : ''}.${this.ChosenFormat.ext}`);

            let formatIds = this.ChosenFormat.format_id.split('+');
            let tempFileNames = [];
            let infos = [];
            let totalSize = 0;

            const getLast = (arr) => arr[arr.length - 1];

            fs.mkdirp(path.join(this.Directory, tempFolder));

            // Main Engine
            const nextStep = () => {
                // Unwatch latest addition
                if (tempFileNames.length > 0) {
                    fs.unwatchFile(path.join(tempPath, getLast(tempFileNames)));
                }

                if (formatIds.length > 0) {
                    this.DownloadSection.progress = -1;
                    this.DownloadSection.status = 'Downloading info...';
                    this.DownloadVideoInfo({ url, format: formatIds.pop(), cwd: tempPath })
                        .then(({ result }) => {
                            console.log({ result });

                            infos.push(JSON.parse(result));
                            totalSize = infos.reduce((acc, curr) => acc + curr.filesize, 0);
                            nextStep();
                        })
                        .catch(handleFailed);
                    return;
                }

                if (infos.length > 0) {
                    console.log({ infos });
                    let info = infos.pop();
                    this.DownloadSection.status = `Downloading ${info.ext} file...`;

                    downloadPart(info).then(nextStep).catch(handleFailed);
                    return;
                }

                if (tempFileNames.length > 1) {
                    this.DownloadSection.status = `Combining media to ${chosenExt}...`;
                    console.log({ totalSize });

                    combine({
                        totalSize: totalSize
                    }).then(() => {
                        tempFileNames = [getLast(tempFileNames)];
                        nextStep();
                    }).catch(handleFailed);
                    return;
                }

                if (chosenExt !== path.extname(getLast(tempFileNames))) {
                    this.DownloadSection.status = `Converting media to ${chosenExt}...`;
                    console.log({ totalSize });
                    convert({
                        totalSize: totalSize
                    }).then(nextStep).catch(handleFailed);
                    return;
                }

                const changeFrom = tempFileNames.pop();
                fs.moveSync(path.join(tempPath, changeFrom), path.join(this.Directory, filename), { overwrite: true });
                fs.removeSync(tempPath);

                // Finished
                this.DownloadSection.progress = 1;
                this.DownloadSection.isFinished = true;
            };

            nextStep();
        },
        StartVideoStream: function ({ url, cwd, filename, format }) {
            return new Promise((resolve, reject) => {
                ipcRenderer.once('client/youtubedl/start_video_stream', (event, args) => {
                    const { error, result } = args;
                    if (error) {
                        reject(error);
                    } else {
                        resolve({ result, args: { url, cwd, filename, format } });
                    }
                });
                ipcRenderer.send('youtubedl/start_video_stream', {
                    cwd,
                    url,
                    filename,
                    format
                });
            });
        },
        DownloadVideoInfo: function ({ url, format, cwd }) {
            return new Promise((resolve, reject) => {
                ipcRenderer.once('client/youtubedl/get_info', (event, args) => {
                    const { error, result } = args;
                    if (error) {
                        reject(error);
                    } else {
                        resolve({ result, args: { url, format, cwd } });
                    }
                });
                ipcRenderer.send('youtubedl/get_info', {
                    cwd,
                    url,
                    format
                });
            });
        },
        FormatDuration: function (ms) {
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
        },
        CombineVideoAndAudio: function ({ cwd, inputs = [], output }) {
            return new Promise((resolve, reject) => {
                ipcRenderer.once('client/ffmpeg/combine', (event, args) => {
                    const { error, result } = args;
                    if (error) {
                        reject(error);
                    } else {
                        resolve({ result, args: { cwd, inputs, output } });
                    }
                });
                ipcRenderer.send('ffmpeg/combine', {
                    cwd,
                    inputs,
                    output
                });
            });
        },
        ConvertFile: function ({ cwd, input, output }) {
            return new Promise((resolve, reject) => {
                ipcRenderer.once('client/ffmpeg/convert', (event, args) => {
                    const { error, result } = args;
                    if (error) {
                        reject(error);
                    } else {
                        resolve({ result, args: { cwd, input, output } });
                    }
                });
                ipcRenderer.send('ffmpeg/convert', {
                    cwd,
                    input,
                    output
                });
            });
        },
        RemoveIllegalFilenameCharacters: function (filename) {
            filename = sanitizeFilename(filename);
            filename = filename.replace('  ', ' ');
            return filename;
        },
        TrackProgress: function ({ filePath, totalSize }) {
            fs.watchFile(filePath, {
                interval: 200
            }, (current, previous) => {
                if (typeof totalSize === 'number') {
                    this.DownloadSection.progress = current.size / totalSize;
                    return;
                }
                this.DownloadSection.progress = 404;
            });
        },
        Abort () {
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
                        label: 'Preset formats',
                        list: []
                    },
                    audioAndVideo: {
                        label: 'Audio and video formats',
                        list: []
                    },
                    video: {
                        label: 'Video only formats',
                        list: []
                    },
                    audio: {
                        label: 'Audio only formats',
                        list: []
                    }
                },
                InfoToDisplay: [
                    { label: 'Title', field: 'fulltitle' },
                    { label: 'Uploader', field: 'uploader' },
                    { label: 'Duration', field: 'duration_formatted' },
                    { label: 'Website', field: 'extractor_key' }
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
.video-thumbnail {
  max-width: 500px;
  display: block;
  margin: 0 auto;
}
.directory {
  display: inline;
}
</style>
