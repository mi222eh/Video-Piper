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

        <!--STATUS-->
        <q-card-section>
          <div v-if="DownloadSection.isFinished" style="color:green">Completed</div>
          <div v-else>{{DownloadSection.status}}</div>
        </q-card-section>

        <q-linear-progress stripe style="height: 15px" :value="this.DownloadSection.progress" />

        <!--DIALOG CLOSE BUTTON-->
        <q-card-actions align="right" class="bg-white text-teal">
          <q-btn flat v-if="DownloadSection.isFinished" label="OK" v-close-popup />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import youtubedl from 'youtube-dl';
import fs from 'fs';
import path from 'path';
import { execFile } from 'child_process';

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

          const bestAudioAndVideoPreset = {
            abr: 50,
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
          const bestAudioPreset = {
            abr: 50,
            acodec: bestAudio.acodec,
            ext: 'mp3',
            filesize: bestAudio.filesize,
            format: 'Best audio',
            format_id: bestAudio.format_id,
            format_note: 'Best audio only',
            quality: -1
          };
          const bestVideoPreset = {
            abr: 50,
            ext: 'mp4',
            filesize: bestVideo.filesize,
            format: 'Best video',
            format_id: bestVideo.format_id,
            format_note: 'Best video only',
            quality: -1,
            height: bestVideo.height,
            vcodec: bestVideo.vcodec
          };

          this.InfoSection.formats.audio.list = audioOnly;
          this.InfoSection.formats.video.list = videoOnly;
          this.InfoSection.formats.audioAndVideo.list = audioAndVideo;

          this.InfoSection.formats.custom.list.push(bestAudioAndVideoPreset);
          this.InfoSection.formats.custom.list.push(bestAudioPreset);
          this.InfoSection.formats.custom.list.push(bestVideoPreset);

          info.formats.unshift(bestAudioAndVideoPreset);
        }
        this.ChosenFormat = info.formats.find(format => format.format_id === info.format_id);
        this.InfoSection.Show = true;
      }).catch((err) => {
        if (isSecondTry) {
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

      const formatIds = this.ChosenFormat.format_id.split('+');
      const isSplit = formatIds.length > 1;
      const filename = `${this.InfoSection.data.fulltitle}${this.ChosenFormat.height ? ` - ${this.ChosenFormat.height}P` : ''}.${this.ChosenFormat.ext}`;

      let filePath = path.join(this.Directory, filename);
      let tempFileNames = [];
      let intervalId;
      let totalDownloaded = [0, 0];
      let infos = [];

      this.DownloadSection.status = 'Getting information';
      this.DownloadVideoInfo(this.CurrentVideoUrl, ['-f', formatIds[0]])
        .then((info) => {
          console.log('download info:', info);
          infos[0] = info;
          tempFileNames[0] = `${info.format_id}.${info.ext}`;

          filePath = path.join(this.Directory, tempFileNames[0]);
          this.DownloadSection.status = 'Downloading';
          intervalId = setInterval(() => {
            fs.stat(filePath, (error, stat) => {
              if (error) return;
              const size = stat.size;
              totalDownloaded[0] = size;
              this.DownloadSection.progress = totalDownloaded.reduce((a, b) => a + b, 0) / this.ChosenFormat.filesize;
            });
          }, 200);
          return this.StartVideoStream(this.CurrentVideoUrl, this.Directory, tempFileNames[0], info.format_id);
        })
        .then(() => {
          clearInterval(intervalId);
          if (!isSplit) return;
          this.DownloadSection.status = 'Getting second part information';
          return this.DownloadVideoInfo(this.CurrentVideoUrl, ['-f', formatIds[1]]);
        }).then((info) => {
          if (!isSplit) return;
          console.log('Second info:', info);
          infos[1] = info;

          tempFileNames[1] = `${info.format_id}.${info.ext}`;
          filePath = path.join(this.Directory, tempFileNames[1]);
          this.DownloadSection.status = 'Downloading second part';
          intervalId = setInterval(() => {
            fs.stat(filePath, (error, stat) => {
              if (error) return;
              const size = stat.size;
              totalDownloaded[1] = size;
              this.DownloadSection.progress = totalDownloaded.reduce((a, b) => a + b, 0) / this.ChosenFormat.filesize;
            });
          }, 200);

          return this.StartVideoStream(this.CurrentVideoUrl, this.Directory, tempFileNames[1], info.format_id);
        }).then((result) => {
          this.DownloadSection.progress = 0;
          if (isSplit) {
            console.log(result);
            clearInterval(intervalId);
            this.DownloadSection.status = 'Combining audio and video';
            tempFileNames[2] = `${infos[0].format_id}${infos[1].format_id}.${this.ChosenFormat.ext}`;

            filePath = path.join(this.Directory, tempFileNames[2]);
            intervalId = setInterval(() => {
              fs.stat(filePath, (error, stat) => {
                if (error) return console.error(error);
                const size = stat.size;
                this.DownloadSection.progress = size / this.ChosenFormat.filesize;
              });
            }, 200);

            return this.CombineVideoAndAudio(this.Directory, tempFileNames[0], tempFileNames[1], tempFileNames[2]);
          } else {
            if (infos[0].ext !== this.ChosenFormat.ext) {
              this.DownloadSection.status = 'Converting to ' + this.ChosenFormat.ext;
              tempFileNames[1] = `${infos[0].format_id}.${this.ChosenFormat.ext}`;
              filePath = path.join(this.Directory, tempFileNames[1]);
              intervalId = setInterval(() => {
                fs.stat(filePath, (error, stat) => {
                  if (error) return console.error(error);
                  const size = stat.size;
                  this.DownloadSection.progress = size / this.ChosenFormat.filesize;
                });
              }, 200);
              return this.ConvertFile(this.Directory, tempFileNames[0], tempFileNames[1]);
            }
          }
        }).then(() => {
          clearInterval(intervalId);
          this.DownloadSection.progress = 1;
          const changeFrom = tempFileNames[tempFileNames.length - 1];
          fs.rename(path.join(this.Directory, changeFrom), path.join(this.Directory, filename), (err) => {
            if (err) return console.error(err);
            tempFileNames.forEach((name) => {
              fs.unlink(path.join(this.Directory, name), (err) => {
                if (err);
              });
            });
          });
        }).catch((err) => {
          if (err) console.error(err);
        }).finally(() => {
          this.DownloadSection.progress = 1;
          this.DownloadSection.isFinished = true;
        });
    },
    StartVideoStream: function (url, directory, filename, format) {
      return new Promise((resolve, reject) => {
        youtubedl.exec(url, ['-f', format, '-o', filename, '--no-part'], {
          cwd: directory
        },
        function Done (err, output) {
          if (err) reject(err);
          else resolve(output);
        });
      });
    },
    DownloadVideoInfo: function (url, args) {
      if (typeof args !== 'object') {
        args = [];
      }

      return new Promise((resolve, reject) => {
        youtubedl.exec(this.CurrentVideoUrl, ['--print-json', '-s', ...args], {}, (err, output) => {
          if (err) {
            reject(err);
          } else {
            const obj = JSON.parse(output);
            obj.duration_formatted = this.FormatDuration(obj.duration * 1000);
            console.log('Info: ', obj);
            resolve(obj);
          }
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
    CombineVideoAndAudio: function (directory, video, audio, filename) {
      return new Promise((resolve, reject) => {
        execFile(path.join(__statics, 'ffmpeg', 'ffmpeg.exe'), ['-i', `"${video}"`, '-i', `"${audio}"`, '-shortest', `"${filename}"`], {
          cwd: directory,
          shell: true
        },
        (err, stdout, stderr) => {
          if (err) return reject(err);
          resolve(stdout);
        });
      });
    },
    ConvertFile: function (directory, input, output) {
      return new Promise((resolve, reject) => {
        execFile(path.join(__statics, 'ffmpeg', 'ffmpeg.exe'), ['-i', `"${input}"`, `"${output}"`], {
          cwd: directory,
          shell: true
        },
        (err, stdout, stderr) => {
          if (err) return reject(err);
          resolve(stdout);
        });
      });
    },
    ShowFormatsDialog: function () {
      this.ShowFormats = true;
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
        status
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
