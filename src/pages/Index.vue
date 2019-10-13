<template>
  <div class="q-pa-md">
    <div class="q-gutter-md">
      <q-card class="get_info_card">
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
          :loading="$store.getters['mediamanager/isGettingVideoInformation']"
          color="primary"
          label="Get Info"
          @click="GetVideoInfo"
        />
      </q-card>
    </div>

    <!--VIDEO INFORMATION CARD-->
    <q-card v-if="!!$store.getters['mediamanager/getVideo']">
      <!--THUMBNAIL-->
      <q-img :src="$store.getters['mediamanager/getVideo'].thumbnail" spinner-color="white" basic class="video-thumbnail">
        <div class="absolute-bottom text-subtitle2 text-center">{{$store.getters['mediamanager/getVideo'].title}}</div>
      </q-img>
      <q-card-section>
        <!--BASIC INFORMATION-->
        <q-card>
          <q-card-section
            v-for="(Info, index) in InfoSection.InfoToDisplay"
            :key="`${index}-${Info.field}`"
          >
            <q-item-label>{{Info.label}}</q-item-label>
            <q-item-label caption lines="2">{{$store.getters['mediamanager/getVideo'][Info.field]}}</q-item-label>
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
        <q-btn color="secondary" label="Download" @click="Download" />
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
        <q-card-section class="scroll">
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
                    <q-item-label>{{format.height ? ` - ${format.height}P` : 'Audio'}}</q-item-label>
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
  </div>
</template>

<script>
import '../store/Mediamanager/doc/MediaManagerDoc';
// const asyncEvery = async function (array, callback) {
//     let ok = true;
//     for (let index = 0; index < array.length; index++) {
//         ok = await callback(array[index], index, array);
//         if (!ok) {
//             index = array.length + 1;
//         }
//     }
//     return ok;
// };
// const TrackProgress = function ({ file, size = 0, totalSize, listener }) {
//     fs.watchFile(
//         file,
//         {
//             interval: 200
//         },
//         (current, previous) => {
//             if (typeof totalSize === 'number' && totalSize > 0) {
//                 let percentage = (current.size + size) / totalSize;
//                 console.log(percentage);

//                 listener({ CurrentProgress: percentage });
//                 return;
//             }
//             listener({ CurrentProgress: 404 });
//         }
//     );
// };
// const UnTrackProgress = function ({ file }) {
//     try {
//         fs.unwatchFile(file);
//     } catch {}
// };
/**
 * @type {String}
 */
let CurrentVideoUrl;

/**
 * @type {VideoInfo}
 */
let currentVideoInfo;
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
            try {
                /**
               * @type {VideoInfo}
               */
                const info = await this.$store.dispatch('mediamanager/getVideoInfo', this.VideoUrl);
                currentVideoInfo = info;
                CurrentVideoUrl = this.VideoUrl;
                console.log(CurrentVideoUrl);
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

                    // info.formats.unshift(bestAudioAndVideoPreset);
                }
                for (const cat in this.InfoSection.formats) {
                    const list = this.InfoSection.formats[cat].list;
                    const chosenFormat = list.find(format => format.format_id === info.format_id);
                    if (chosenFormat) {
                        this.ChosenFormat = chosenFormat;
                    }
                }
            } catch (err) {
                this.$q.notify({
                    message: err.message,
                    color: 'negative',
                    timeout: 1000
                });
            }
        },
        Download: async function () {
            try {
                /**
               * @type {VideoTask}
               */
                const task = {
                    chosenFormat: this.ChosenFormat.format_id.split('+'),
                    chosenExtension: this.ChosenFormat.ext,
                    folder: this.Directory,
                    URL: CurrentVideoUrl,
                    id: currentVideoInfo.extractor_key + currentVideoInfo.id + this.ChosenFormat.format_id,
                    thumbnail: currentVideoInfo.thumbnail,
                    title: currentVideoInfo.fulltitle
                };
                await this.$store.dispatch('mediamanager/addVideoToTask', task);
            } catch (error) {
                this.$q.notify({
                    message: error.message,
                    color: 'negative',
                    timeout: 1000
                });
            }
        }
        // HandleFailed: function ({ message, tempPath }) {
        //     console.log({ message, tempPath });

        //     if (tempPath) {
        //         removeFolder({ tempPath });
        //     }

        //     this.DownloadSection.failed = true;
        //     this.DownloadSection.isFinished = true;
        //     this.DownloadSection.errorMessage = message;
        //     this.DownloadSection.progress = 1;
        // },
        // DownloadVideo: async function () {
        //     if (!this.Directory) {
        //         this.$q.notify('Please choose a directory');
        //         return;
        //     }
        //     if (!this.ChosenFormat) {
        //         this.$q.notify('Please choose a format');
        //         return;
        //     }

        //     const HandleFailed = this.HandleFailed;

        //     this.DownloadSection.status = 'Preparing...';
        //     this.ShowDownloadDialog = true;
        //     this.DownloadSection.isFinished = false;
        //     this.DownloadSection.progress = -1;
        //     this.DownloadSection.failed = false;
        //     const url = this.CurrentVideoUrl;
        //     const directory = this.Directory;
        //     const chosenExt = this.ChosenFormat.ext;
        //     const filename = RemoveIllegalFilenameCharacters(
        //         `${this.InfoSection.data.fulltitle}${
        //             this.ChosenFormat.height ? ` - ${this.ChosenFormat.height}P` : ''
        //         }.${this.ChosenFormat.ext}`
        //     );

        //     // Get info section
        //     this.DownloadSection.status = 'Getting information...';
        //     let formatIds = Factory.Format(this.ChosenFormat).formatId.split('+');
        //     let infos = [];
        //     while (formatIds.length > 0) {
        //         const { error, result } = await handlePromise(
        //             downloadInfo({ url, format: formatIds.pop(), cwd: directory })
        //         );
        //         if (error) {
        //             this.HandleFailed({ error });
        //             return;
        //         }
        //         infos.push(Factory.Format(JSON.parse(result)));
        //     }
        //     console.log(infos);

        //     const totalSize = infos.reduce((acc, curr) => acc + curr.filesize, 0);
        //     // Create folder
        //     const tempFolder = infos[0].id;
        //     await fs.mkdirp(path.join(directory, tempFolder));

        //     // Download media(s)
        //     let tempFileNames = [];
        //     let totalDownloaded = 0;
        //     this.DownloadSection.status = 'Piping media...';
        //     let isOk = await asyncEvery(infos, async info => {
        //         let tempName = `${info.formatId}.${info.ext}`;
        //         const currentTempFilePath = path.join(directory, tempFolder, tempName);
        //         tempFileNames.push(tempName);
        //         TrackProgress({
        //             file: currentTempFilePath,
        //             size: totalDownloaded,
        //             totalSize: totalSize,
        //             listener: ({ CurrentProgress }) => {
        //                 this.DownloadSection.progress = CurrentProgress;
        //             }
        //         });
        //         const { error } = await handlePromise(
        //             downloadMedia({
        //                 url,
        //                 cwd: path.join(directory, tempFolder),
        //                 filename: tempName,
        //                 format: info.formatId
        //             })
        //         );
        //         UnTrackProgress({ file: currentTempFilePath });
        //         if (error) {
        //             HandleFailed({
        //                 message: `Failed to download ${info.format}`,
        //                 tempPath: fs.join(directory, tempFolder)
        //             });
        //             return false;
        //         }
        //         totalDownloaded += info.filesize;
        //         return true;
        //     });
        //     if (!isOk) {
        //         return;
        //     }
        //     // Combining section
        //     if (tempFileNames.length > 1) {
        //         this.DownloadSection.status = 'Combining media...';
        //         const tempFilename = `${tempFileNames.join('')}.${chosenExt}`;
        //         const tempFilePath = path.join(directory, tempFolder, tempFilename);
        //         TrackProgress({
        //             file: tempFilePath,
        //             size: 0,
        //             totalSize: infos.reduce((acc, curr) => acc + curr.filesize, 0),
        //             listener: ({ CurrentProgress }) => {
        //                 this.DownloadSection.progress = CurrentProgress;
        //             }
        //         });
        //         const { error } = await handlePromise(
        //             combine({
        //                 cwd: path.join(directory, tempFolder),
        //                 inputs: tempFileNames,
        //                 output: tempFilename
        //             })
        //         );
        //         UnTrackProgress({ file: tempFilePath });
        //         if (error) {
        //             HandleFailed({
        //                 message: error,
        //                 tempPath: path.join(directory, tempFolder)
        //             });
        //             console.error(error);
        //             return;
        //         }
        //         tempFileNames.push(tempFilename);
        //     }
        //     const last = getLast(tempFileNames);

        //     // Convert section
        //     if (path.extname(last) !== `.${chosenExt}`) {
        //         this.DownloadSection.status = 'Converting media...';
        //         const tempFilename = `${last}.${chosenExt}`;
        //         const tempFilePath = path.join(directory, tempFolder, tempFilename);
        //         TrackProgress({
        //             file: tempFilePath,
        //             size: 0,
        //             totalSize: infos.reduce((acc, curr) => acc + curr.filesize, 0),
        //             listener: ({ CurrentProgress }) => {
        //                 this.DownloadSection.progress = CurrentProgress;
        //             }
        //         });
        //         const { error } = await handlePromise(
        //             convert({
        //                 cwd: path.join(directory, tempFolder),
        //                 input: last,
        //                 output: tempFilename
        //             })
        //         );
        //         UnTrackProgress({
        //             file: tempFilePath
        //         });
        //         if (error) {
        //             HandleFailed({
        //                 message: error,
        //                 tempPath: path.join(directory, tempFolder)
        //             });
        //             console.error(error);
        //             return;
        //         }
        //     }

        //     fs.moveSync(
        //         path.join(directory, tempFolder, getLast(tempFileNames)),
        //         path.join(directory, filename)
        //     );
        //     fs.remove(path.join(directory, tempFolder));

        //     this.DownloadSection.progress = 1;
        //     this.DownloadSection.isFinished = true;
        // },
        // Abort: function () {
        //     ipcRenderer.send('abort');
        // }
    },
    data () {
        return {
            VideoUrl: '',
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
            }
        };
    }
};
</script>

<style>
.get_info_button {
  margin-top: 10px;
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
