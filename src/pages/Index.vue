<template>
  <div class="q-pa-md">
    <div class="q-gutter-md" style="max-width: 80%">
      <q-input ref="DirectoryInput" v-model="Directory" readonly label="Output Directory" />
      <q-btn color="secondary" label="Choose directory" @click="ChooseDirectory" />
      <q-input ref="URLInput" v-model="VideoUrl" label="Video URL" />
      <q-btn
        :loading="IsGettingVideoInformation"
        color="primary"
        label="Get Info"
        @click="GetVideoInfo"
      />
    </div>
    <q-card v-if="InfoSection.Show">
        <q-img :src="InfoSection.data.thumbnail" spinner-color="white" basic class="video-thumbnail">
          <div class="absolute-bottom text-subtitle2 text-center">{{InfoSection.data.title}}</div>
        </q-img>
      <q-card-section>
        <div v-for="(Info, index) in InfoSection.InfoToDisplay" :key="`${index}-${Info.field}`">
          <q-input standout v-model="InfoSection.data[Info.field]" readonly :label="Info.label" />
        </div>
        <q-select
        outlined
        label="Choose Format"
        v-model="ChosenFormat"
        :options="InfoSection.data.formats"
        :option-value="opt => opt === null ? null : opt"
        :option-label="opt => opt === null ? '- Null -' : opt.format"
        map-options
        />
        <q-input standout :value="`${ChosenFormat.filesize / 1000000} MB`" readonly label="Size" />
        <q-input standout :value="ChosenFormat.ext" readonly label="Extension" />
        <q-input standout :value="ChosenFormat.acodec" readonly label="Audio type" />
        <q-input standout :value="ChosenFormat.vcodec" readonly label="Video type" />

      </q-card-section>
      <q-card-section>
        <q-btn color="secondary" label="Download" @click="DownloadVideo" />
      </q-card-section>
    </q-card>

    <q-dialog
      v-model="ShowDownloadDialog"
      persistent
      transition-show="scale"
      transition-hide="scale"
    >
      <q-card style="width: 300px">
        <q-card-section>
          <div class="text-h6">Download Started</div>
          <q-linear-progress stripe style="height: 15px" :value="this.DownloadSection.progress" />
        </q-card-section>

        <q-card-section>
          <div v-if="DownloadSection.isFinished" style="color:green">Completed</div>
          <div v-else>{{DownloadSection.status}}</div>
        </q-card-section>

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
    GetVideoInfo: function () {
      if (!this.VideoUrl) {
        return;
      }
      // Set variables
      this.CurrentVideoUrl = this.VideoUrl;
      this.IsGettingVideoInformation = true;
      this.InfoSection.Show = false;

      // Begin downloading the video information with the default being bestvideo+bestaudio
      this.DownloadVideoInfo(this.CurrentVideoUrl, ['-f', 'bestvideo+bestaudio']).then((info) => {
        // Set variables
        this.IsGettingVideoInformation = false;
        this.InfoSection.data = info;

        // Split format id
        const formatIds = info.format_id.split('+');
        if (formatIds.length > 1) {
          const bestVideo = info.formats.find(format => formatIds.some(formatId => formatId === format.format_id) && format.vcodec !== 'none');
          const bestAudio = info.formats.find(format => formatIds.some(formatId => formatId === format.format_id) && format.acodec !== 'none');
          const format = {
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
          info.formats.unshift(format);
        }
        this.ChosenFormat = info.formats.find(format => format.format_id === info.format_id);
        this.InfoSection.Show = true;
      }).catch((err) => {
        console.error(err);
      });
    },
    DownloadVideo: function () {
      if (!this.Directory) {
        return;
      }
      if (!this.ChosenFormat) {
        return;
      }

      this.ShowDownloadDialog = true;
      this.DownloadSection.isFinished = false;
      this.DownloadSection.progress = 0;

      const formatIds = this.ChosenFormat.format_id.split('+');
      const isSplit = formatIds.length > 1;
      const filename = `${this.InfoSection.data.fulltitle}-${this.ChosenFormat.height}P.${this.ChosenFormat.ext}`.replace('|', '').replace('  ', ' ');
      let filePath = path.join(this.Directory, filename);

      let tempFileNames = [];
      let intervalId;
      let totalDownloaded = [0, 0];

      this.DownloadSection.status = 'Getting video information';
      this.DownloadVideoInfo(this.CurrentVideoUrl, ['-f', formatIds[0]])
        .then((info) => {
          console.log('download info:', info);

          if (isSplit) {
            tempFileNames[0] = `${info.format_id}.${info.ext}`;
          } else {
            tempFileNames[0] = `${this.InfoSection.data.fulltitle}-${this.ChosenFormat.height}P.${this.ChosenFormat.ext}`;
          }
          filePath = path.join(this.Directory, tempFileNames[0]);
          this.DownloadSection.status = 'Downloading video';
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
        .then((result) => {
          console.log(result);
          clearInterval(intervalId);
          if (!isSplit) {
            throw String('');
          }
          return '';
        }).then(() => {
          this.DownloadSection.status = 'Getting audio information';
          return this.DownloadVideoInfo(this.CurrentVideoUrl, ['-f', formatIds[1]]);
        }).then((info) => {
          console.log('Audio info:', info);

          tempFileNames[1] = `${info.format_id}.${info.ext}`;
          filePath = path.join(this.Directory, tempFileNames[1]);
          this.DownloadSection.status = 'Downloading audio';
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
          console.log(result);
          clearInterval(intervalId);
          this.DownloadSection.progress = 0;
          this.DownloadSection.status = 'Combining audio and video';

          filePath = path.join(this.Directory, filename);
          intervalId = setInterval(() => {
            fs.stat(filePath, (error, stat) => {
              if (error) return console.error(error);
              const size = stat.size;
              this.DownloadSection.progress = size / this.ChosenFormat.filesize;
            });
          }, 200);

          return this.CombineVideoAndAudio(this.Directory, tempFileNames[0], tempFileNames[1], filename);
        }).then(() => {
          clearInterval(intervalId);
          tempFileNames.forEach((name) => {
            fs.unlink(path.join(this.Directory, name), (err) => {
              if (err) console.error(err);
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
    }
  },
  data () {
    return {
      VideoUrl: '',
      IsGettingVideoInformation: false,
      Video: {},
      CurrentVideoUrl: '',
      ShowDownloadDialog: false,
      ChosenFormat: {},
      Directory: '',
      InfoSection: {
        Show: false,
        data: {},
        InfoToDisplay: [
          { label: 'Title', field: 'fulltitle' },
          { label: 'Uploader', field: 'uploader' },
          { label: 'Duration', field: 'duration_formatted' }
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
</style>
