<template>
  <div class="q-pa-md">
    <div class="q-gutter-md" style="max-width: 80%">
      <q-input
      ref="DirectoryInput"
      v-model="Directory"
      readonly
      label="Output Directory"/>
      <q-btn
        color="secondary"
        label="Choose directory"
        @click="ChooseDirectory"
      />
      <q-input
      ref="URLInput"
      v-model="VideoUrl"
      label="Video URL"/>
      <q-btn
        :loading="IsGettingVideoInformation"
        color="primary"
        label="Get Info"
        @click="GetVideoInfo"
      />
    </div>
    <q-card v-if="InfoSection.Show">
      <q-card-section>
        <div class="text-h6">Video Information</div>
      </q-card-section>
      <q-card-section>
        <q-input standout v-model="CurrentVideoInfo.title" readonly label="Title" />
        <q-input standout v-model="CurrentVideoInfo._duration_hms" readonly label="Duration" />
        <q-input standout v-model="CurrentVideoInfo.resolution" readonly label="Resolution" />
      </q-card-section>
      <q-card-section>
        <q-btn color="secondary" label="Download Video" @click="DownloadVideo" />
      </q-card-section>
    </q-card>

    <q-dialog v-model="ShowDownloadDialog" persistent transition-show="scale" transition-hide="scale">
      <q-card style="width: 300px">
        <q-card-section>
          <div class="text-h6">Download Started</div>
          <q-linear-progress stripe style="height: 15px" :value="DownloadProgress" />
                <!-- <q-spinner
                v-if="!IsDownloadFinished"
                  color="primary"
                  size="3em"
                /> -->
        </q-card-section>

        <q-card-section>
          <div v-if="IsDownloadFinished" class="text-color-green">
            Download Completed
          </div>
          <q-input standout v-model="CurrentVideoInfo._filename" readonly label="File name" />
          <q-input standout v-model="CurrentVideoInfo.size" readonly label="Size in bytes" />
        </q-card-section>

        <q-card-actions align="right" class="bg-white text-teal">
          <q-btn flat v-if="IsDownloadFinished" label="OK" v-close-popup />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script>
import youtube from 'youtube-dl';
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
      let Video = youtube(this.VideoUrl);
      Video.on('info', info => {
        this.IsGettingVideoInformation = false;
        this.CurrentVideoInfo = info;
        this.InfoSection.Show = true;
        console.log(info);
      });
    },
    DownloadVideo: function () {
      if (!this.Directory) {
        return;
      }
      let video = youtube(this.CurrentVideoUrl);
      this.ShowDownloadDialog = true;
      this.IsDownloadFinished = false;
      this.DownloadProgress = 0;
      let filePath = path.join(this.Directory, this.CurrentVideoInfo._filename);
      video.on('info', (info) => {
        this.DownloadInfo = info;
        console.log('Download started');
        console.log('filename: ' + info._filename);
        console.log('size: ' + info.size);
      });
      video.on('end', (info) => {
        this.IsDownloadFinished = true;
        this.DownloadProgress = 1;
        clearInterval(timeoutId);
      });
      let timeoutId = setInterval(() => {
        fs.stat(filePath, (error, stat) => {
          if (error) return;
          console.log(stat);
          let size = stat.size;
          this.DownloadProgress = size / this.CurrentVideoInfo.size;
        });
      }, 200);

      video.pipe(fs.createWriteStream(filePath));
    }
  },
  data () {
    return {
      VideoUrl: '',
      IsGettingVideoInformation: false,
      Video: {},
      CurrentVideoUrl: '',
      CurrentVideoInfo: {},
      ShowDownloadDialog: false,
      IsDownloadFinished: false,
      DownloadProgress: 0,
      Directory: '',
      InfoSection: {
        Show: false
      },
      DownloadInfo: {}
    };
  }
};
</script>

<style>
</style>
