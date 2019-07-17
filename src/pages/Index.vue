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
        <q-btn color="secondary" label="Download Video" @click="DownloadVideo" />
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
          <div v-if="IsDownloadFinished" style="color:green">Download Completed</div>
        </q-card-section>

        <q-card-actions align="right" class="bg-white text-teal">
          <q-btn flat v-if="IsDownloadFinished" label="OK" v-close-popup />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import youtubedl from 'youtube-dl';
import fs from 'fs';
import path from 'path';

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
      this.CurrentVideoUrl = this.VideoUrl;
      this.IsGettingVideoInformation = true;
      this.InfoSection.Show = false;
      youtubedl.getInfo(this.CurrentVideoUrl, ['-f', 'bestvideo+bestaudio']);
      this.DownloadVideoInfo(this.CurrentVideoUrl).then((info) => {
        this.IsGettingVideoInformation = false;
        this.InfoSection.data = info;
        this.ChosenFormat = info.formats.find(format => format.format_id === info.format_id);
        this.InfoSection.Show = true;

        console.log('info: ', this.InfoSection.data);
        console.log('Format: ', this.ChosenFormat);
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
      this.IsDownloadFinished = false;
      this.DownloadSection.progress = 0;
      const filename = `${this.InfoSection.data.fulltitle}-${this.ChosenFormat.height}P.${this.ChosenFormat.ext}`;
      const filePath = path.join(this.Directory, filename);
      let downloadVideoInfo;
      let intervalId;
      this.DownloadVideoInfo(this.CurrentVideoUrl, ['-f', this.ChosenFormat.format_id])
        .then((info) => {
          downloadVideoInfo = info;
          intervalId = setInterval(() => {
            fs.stat(filePath, (error, stat) => {
              if (error) return;
              const size = stat.size;
              this.DownloadSection.progress = size / downloadVideoInfo.size;
            });
          }, 200);
          console.log('download info:', downloadVideoInfo);
          return this.StartVideoStream(this.CurrentVideoUrl, this.Directory, filename, this.ChosenFormat.format_id);
        })
        .then((result) => {
          console.log(result);
          clearInterval(intervalId);
          this.DownloadSection.progress = 1;
          this.IsDownloadFinished = true;
        }).catch((err) => {
          console.error(err);
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
      // youtubedl.exec(this.CurrentVideoUrl, ['-F', '-j'], {}, (err, output) => {
      //   if (err) returnconsole.error(err);

      // })
      return new Promise((resolve, reject) => {
        const video = youtubedl(url, args);
        video.on('info', (info) => {
          info.formats.forEach(format => {
            if (!format.filesize) {
              format.filesize = info.size;
            }
          });
          resolve(info);
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
      IsDownloadFinished: false,
      DownloadProgress: 0,
      ChosenFormat: {},
      Directory: '',
      InfoSection: {
        Show: false,
        data: {},
        InfoToDisplay: [
          { label: 'Title', field: 'fulltitle' },
          { label: 'Uploader', field: 'uploader' },
          { label: 'Duration', field: '_duration_hms' }
        ]
      },
      DownloadSection: {
        data: {},
        progress: 0
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
