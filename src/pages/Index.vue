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
          <q-card-section v-for="(Info, index) in InfoSection.InfoToDisplay" :key="`${index}-${Info.field}`">
            <q-item-label>{{Info.label}}</q-item-label>
            <q-item-label caption lines="2">{{InfoSection.data[Info.field]}}</q-item-label>
          </q-card-section>
        </q-card>
        <q-card>
          <q-item clickable v-ripple @click="ShowFormatsDialog">
            <q-item-section>
              <q-item-label>Format{{ChosenFormat.height ? ` - ${ChosenFormat.height}P` : ''}}</q-item-label>
              <q-item-label caption>{{ChosenFormat.format}}{{ChosenFormat.filesize ? ' at ' + (Math.round(ChosenFormat.filesize / 1000000)) + 'MB': ''}}</q-item-label>
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
    <q-dialog
    v-model="ShowFormats"
    transition-show="scale"
    transition-hide="scale">
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
              v-for="(category, index) in InfoSection.formats" :key="`${index}-Category`"
              expand-separator
              :label="`${category.label}`"
              caption=""
            >
            <q-list v-for="(format, index) in category.list" :key="`${index}-format`">
              <q-item clickable v-close-popup v-ripple @click="ChosenFormat = format" :active="ChosenFormat.format_id === format.format_id">
                <q-item-section>
                  <q-item-label>Format{{format.height ? ` - ${format.height}P` : ''}}</q-item-label>
                  <q-item-label caption>{{format.format}}{{format.filesize ? ' at ' + (Math.round(format.filesize / 1000000)) + 'MB': ''}}</q-item-label>
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

        <q-linear-progress v-if="DownloadSection.progress === -1" query style="height: 15px" :value="this.DownloadSection.progress" />

        <q-linear-progress v-else-if="DownloadSection.progress === 404 " indeterminate stripe style="height: 15px" :value="this.DownloadSection.progress" />

        <q-linear-progress v-else stripe style="height: 15px" :value="DownloadSection.progress" />

        <q-separator />

        <!--STATUS-->
        <q-card-section>
          <div v-if="DownloadSection.isFinished">
            <div v-if="DownloadSection.failed">
              Error: {{DownloadSection.errorMessage}}
            </div>
            <div v-else style="color:green">
              Completed
            </div>
            </div>
          <div v-else>{{DownloadSection.status}}</div>
        </q-card-section>

        <!--DIALOG CLOSE BUTTON-->
        <q-card-actions align="right" class="bg-white text-teal">
          <q-btn flat v-if="DownloadSection.isFinished" label="OK" v-close-popup />
          <q-btn flat v-else label="Cancel" @click="DownloadSection.closeCurrentProcess('User cancelled')"/>
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import fs from 'fs-extra';
import path from 'path';
import { execFile } from 'child_process';
import terminate from 'terminate';
import sanitizeFilename from 'sanitize-filename';

const Utils = {
    ExecuteYoutubeDl: function ({ cwd, url, args, callback }) {
        const youtubedl = path.join(__statics, 'youtube-dl', 'bin', 'youtube-dl.exe');
        return execFile(youtubedl, [...args, url], {
            cwd: cwd,
            maxBuffer: 1024 * 1024 * 10
        }, callback
        );
    }
};

export default {
    name: 'PageIndex',
    created () {},
    methods: {
        ChooseDirectory: function () {
            this.Directory = this.$q.electron.remote.dialog.showOpenDialog({
                properties: ['openDirectory']
            })[0];
        },
        GetVideoInfo: function (isSecondTry) {
            if (!this.VideoUrl) {
                this.$q.notify('Please input the video url');
                return;
            }
            // Set variables
            this.CurrentVideoUrl = this.VideoUrl;
            this.IsGettingVideoInformation = true;
            this.InfoSection.Show = false;

            let args = [];
            if (!isSecondTry) {
                args = ['-f', 'bestvideo+bestaudio'];
            }
            // Begin downloading the video information with the default being bestvideo+bestaudio
            this.DownloadVideoInfo(this.CurrentVideoUrl, args).then((info) => {
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
                        console.log('acc: ', accumulator);
                        console.log('acurr: ', currentValue);
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
            }).catch((err) => {
                if (isSecondTry) {
                    this.$q.notify('Error getting video information');
                    this.IsGettingVideoInformation = false;
                    return console.error(err);
                }
                this.DownloadVideoInfo(true);
            });
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
            this.DownloadSection.isFinished = false;
            this.DownloadSection.progress = 0;
            this.DownloadSection.failed = false;

            const formatIds = this.ChosenFormat.format_id.split('+');
            const isSplit = formatIds.length > 1;
            const tempFolder = this.InfoSection.data.id;
            const filename = this.RemoveIllegalFilenameCharacters(`${this.InfoSection.data.fulltitle}${this.ChosenFormat.height ? ` - ${this.ChosenFormat.height}P` : ''}.${this.ChosenFormat.ext}`);

            let tempFileNames = [];
            let infos = [];

            const getLatestFileName = () => tempFileNames[tempFileNames.length - 1];

            fs.mkdirp(path.join(this.Directory, tempFolder));

            this.DownloadSection.progress = -1;
            this.DownloadSection.status = 'Preparing';

            const downloadPart = (formatId) => {
                return new Promise((downloadResolve, downloadReject) => {

                });
            };

            const downloadParts = () => {
                if (formatIds.length > 0) {
                    downloadPart(formatIds.pop()).then(downloadParts).catch(err => console.error(err));
                }
            };

            // Download information
            this.DownloadVideoInfo(this.CurrentVideoUrl, ['-f', formatIds[0]])
                .then((info) => {
                    console.log('download info:', info);
                    infos[0] = info;
                    tempFileNames[0] = `${info.id}-${info.format_id}.${info.ext}`;

                    this.DownloadSection.status = 'Downloading';
                    fs.watchFile(path.join(this.Directory, tempFolder, tempFileNames[0]), {
                        interval: 200
                    }, (current, previous) => {
                        if (info.filesize) {
                            this.DownloadSection.progress = current.size / info.filesize;
                            return;
                        }
                        this.DownloadSection.progress = 404;
                    });

                    // Stream into file
                    return this.StartVideoStream(this.CurrentVideoUrl, path.join(this.Directory, tempFolder), tempFileNames[0], info.format_id);
                })
                .then(() => {
                    fs.unwatchFile(path.join(this.Directory, tempFolder, tempFileNames[0]));
                    if (!isSplit) return;
                    this.DownloadSection.progress = 0;
                    this.DownloadSection.status = 'Getting second part information';
                    this.DownloadSection.progress = -1;

                    // Getting information for the second part
                    return this.DownloadVideoInfo(this.CurrentVideoUrl, ['-f', formatIds[1]]);
                }).then((info) => {
                    if (!isSplit) return;
                    console.log('Second info:', info);
                    infos[1] = info;

                    tempFileNames[1] = `${info.id}-${info.format_id}.${info.ext}`;
                    this.DownloadSection.status = 'Downloading second part';
                    fs.watchFile(path.join(this.Directory, tempFolder, tempFileNames[1]), {
                        interval: 200
                    }, (current, previous) => {
                        if (info.filesize) {
                            this.DownloadSection.progress = current.size / info.filesize;
                            return;
                        }
                        this.DownloadSection.progress = current.size / info.filesize;
                    });

                    // Stream into file
                    return this.StartVideoStream(this.CurrentVideoUrl, path.join(this.Directory, tempFolder), tempFileNames[1], info.format_id);
                }).then(async (result) => {
                    this.DownloadSection.progress = 0;
                    if (isSplit) {
                        this.DownloadSection.status = 'Combining parts to ' + this.ChosenFormat.ext;
                        fs.unwatchFile(path.join(this.Directory, tempFolder, tempFileNames[1]));
                        tempFileNames[2] = `${infos[0].id}-${infos[0].format_id}-${infos[1].format_id}.${this.ChosenFormat.ext}`;
                        await fs.remove(path.join(this.Directory, tempFolder, tempFileNames[2]));
                        fs.watchFile(path.join(this.Directory, tempFolder, tempFileNames[2]), {
                            interval: 200
                        }, (current, previous) => {
                            this.DownloadSection.progress = current.size / (infos[0].filesize + infos[1].filesize);
                        });
                        return this.CombineVideoAndAudio(path.join(this.Directory, tempFolder), tempFileNames[0], tempFileNames[1], tempFileNames[2]);
                    } else {
                        if (infos[0].ext !== this.ChosenFormat.ext) {
                            this.DownloadSection.status = 'Converting to ' + this.ChosenFormat.ext;
                            tempFileNames[1] = `${infos[0].id}-${infos[0].format_id}.${this.ChosenFormat.ext}`;
                            await fs.remove(path.join(this.Directory, tempFolder, tempFileNames[1]));
                            fs.watchFile(path.join(this.Directory, tempFolder, tempFileNames[1]), {
                                interval: 200
                            }, (current, previous) => {
                                this.DownloadSection.progress = current.size / infos[0].filesize;
                            });
                            return this.ConvertFile(path.join(this.Directory, tempFolder), tempFileNames[0], tempFileNames[1]);
                        }
                    }
                }).then(() => {
                    fs.unwatchFile(path.join(this.Directory, tempFolder, tempFileNames[tempFileNames.length - 1]));
                    const changeFrom = tempFileNames.pop();
                    return fs.move(path.join(this.Directory, tempFolder, changeFrom), path.join(this.Directory, filename));
                }).then(() => {
                    return fs.remove(path.join(this.Directory, tempFolder));
                }).catch((err) => {
                    tempFileNames.forEach((fileName) => {
                        try {
                            fs.unwatchFile(path.join(this.Directory, tempFolder, fileName));
                        } catch (err) {}
                    });
                    console.error(err);
                    this.DownloadSection.failed = true;
                    this.DownloadSection.errorMessage = err.toString();
                }).finally(() => {
                    this.DownloadSection.progress = 1;
                    this.DownloadSection.isFinished = true;
                });
        },
        StartVideoStream: function ({ url, directory, filename, format }) {
            return new Promise((resolve, reject) => {
                const cp = Utils.ExecuteYoutubeDl({
                    cwd: this.Directory,
                    url: this.CurrentVideoUrl,
                    args: ['-f', format, '-o', filename, '--no-part'],
                    callback: (err, out) => {
                        this.DownloadSection.closeCurrentProcess = () => {};
                        if (err) {
                            return reject(err);
                        }
                        resolve(out);
                    }
                });
                this.DownloadSection.closeCurrentProcess = (message) => {
                    terminate(cp.pid);
                    reject(message);
                };
            });
        },
        DownloadVideoInfo: function ({ url, args }) {
            if (typeof args !== 'object') {
                args = [];
            }

            return new Promise((resolve, reject) => {
                const cp = Utils.ExecuteYoutubeDl({
                    url: url,
                    args: ['--print-json', '-s', ...args],
                    callback: (err, out) => {
                        this.DownloadSection.closeCurrentProcess = () => {};
                        if (err) {
                            reject(err);
                        } else {
                            const obj = JSON.parse(out);
                            obj.duration_formatted = this.FormatDuration(obj.duration * 1000);
                            console.log('Info: ', obj);
                            resolve(obj);
                        }
                    }
                });
                this.DownloadSection.closeCurrentProcess = (message) => {
                    terminate(cp.pid);
                    reject(message);
                };
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
        CombineVideoAndAudio: function (directory, video, audio, filename) {
            return new Promise((resolve, reject) => {
                const cp = execFile(path.join(__statics, 'ffmpeg', 'ffmpeg.exe'), ['-i', `"${video}"`, '-i', `"${audio}"`, '-shortest', `"${filename}"`], {
                    cwd: directory,
                    shell: true,
                    maxBuffer: 1024 * 1024 * 10
                },
                (err, stdout, stderr) => {
                    this.DownloadSection.closeCurrentProcess = () => {};
                    if (err) return reject(err);
                    resolve(stdout);
                });
                this.DownloadSection.closeCurrentProcess = (message) => {
                    terminate(cp.pid);
                    reject(message);
                };
            });
        },
        ConvertFile: function (directory, input, output) {
            return new Promise((resolve, reject) => {
                const cp = execFile(path.join(__statics, 'ffmpeg', 'ffmpeg.exe'), ['-i', `"${input}"`, `"${output}"`], {
                    cwd: directory,
                    shell: true,
                    maxBuffer: 1024 * 1024 * 10
                },
                (err, stdout, stderr) => {
                    this.DownloadSection.closeCurrentProcess = () => {};
                    if (err) return reject(err);
                    resolve(stdout);
                });
                this.DownloadSection.closeCurrentProcess = (message) => {
                    terminate(cp.pid);
                    reject(message);
                };
            });
        },
        ShowFormatsDialog: function () {
            this.ShowFormats = true;
        },
        RemoveIllegalFilenameCharacters: function (filename) {
            filename = sanitizeFilename(filename);
            filename = filename.replace('  ', ' ');
            return filename;
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
                errorMessage: '',
                closeCurrentProcess: function () {}
            }
        };
    }
};
</script>

<style>
  .video-thumbnail{
    max-width: 500px;
    display: block;
    margin: 0 auto;
  }
  .directory{
    display: inline;
  }
</style>
