<template>
  <main>
    <!--DIRECTORY INPUT-->
    <q-item clickable v-ripple @click="ChooseDirectory">
      <q-item-section avatar top>
        <q-avatar icon="folder" color="primary" text-color="white" />
      </q-item-section>
      <q-item-section>
        <q-item-label>Output Directory</q-item-label>
        <q-item-label caption>
          {{
          $store.getters['index/folder']
          }}
        </q-item-label>
      </q-item-section>
    </q-item>
    <!--URL INPUT-->
    <q-input stack-label v-model="url" label="Playlist URL" />
    <!--GET INFO BUTTON-->
    <div v-if="$store.getters['playlist/GetIsBusy']">
      Please wait, getting entry {{$store.getters['playlist/GetNumberOfFinished'] + 1}}/{{$store.getters['playlist/GetTotalNumberOfVideos']}}
    </div>
    <q-btn v-if="!$store.getters['playlist/GetIsBusy']" :loading="IsGettingInfo" color="primary" label="Get Info" @click="getPlaylistInfo" />
    <q-card v-if="!!info.playlistData" class="playlist-info">
      <!--BASIC INFORMATION-->
      <q-card-section>
        <q-item-label>Title</q-item-label>
        <q-item-label caption lines="2">{{info.playlistData.title}}</q-item-label>
      </q-card-section>
      <q-expansion-item
        switch-toggle-side
        expand-separator
        icon="list"
        :label="`Entries: ${info.playlistData.entries.length}`"
      >
        <q-virtual-scroll class="playlist" :items="info.playlistData.entries" type="table">
          <template v-slot:before>
            <thead class="playlist-header">
              <tr>
                <th class="playlist-head">Index</th>
                <th
                  class="playlist-head"
                  v-for="col in info.columns"
                  :key="'header--' + col.name"
                >{{ col.header }}</th>
              </tr>
            </thead>
          </template>
          <template v-slot="{item, index}">
            <tr :key="`playlist${index}`">
              <td>#{{ index }}</td>
              <td v-for="col in info.columns" :key="index + '-' + col.field">{{ item[col.field] }}</td>
            </tr>
          </template>
        </q-virtual-scroll>
      </q-expansion-item>
      <h5 class="text-center">Format selection</h5>
      <q-btn-toggle
        v-model="info.baseFormat"
        push
        glossy
        toggle-color="teal"
        class="flex-center q-pa-md"
        :options="[
          {label: 'Best', value: 'best'},
          {label: 'Best video and audio', value: 'bestvideo+bestaudio'},
          {label: 'Best audio', value: 'bestaudio'},
          {label: 'Best video', value: 'bestvideo'}
        ]"
      ></q-btn-toggle>
      <div class="flex flex-row flex-center" v-if="containsVideo(info.baseFormat)">
        <q-toggle v-model="info.maxResEnabled" label="Enabled max resolution" />
        <q-input
          v-model="info.maxHeight"
          label="height"
          class="q-pa-md"
          stack-label
          :disable="!info.maxResEnabled"
        />
      </div>
      <div class="flex flex-row flex-center" v-if="!containsVideo(info.baseFormat)">
        <q-toggle v-model="info.convertToMP3" label="Convert to MP3" />
      </div>
      <q-btn
        :loading="info.isGettingPlaylist"
        color="secondary"
        label="Get Playlist"
        @click="downloadPlaylist"
      />
    </q-card>
  </main>
</template>

<script lang="ts">
import Vue from 'vue';
import { VideoPlaylist } from '../store/types/VideoPlaylist';
import { PlaylistTask } from '../store/playlist/playlistTypes';

interface Playlist {
  IsGettingInfo: boolean;
  url: string;
  info: {
    currentUrl: string;
    playlistData: VideoPlaylist | null;
    listExpanded: boolean;
    columns: { field: string; header: string }[];
    baseFormat: string;
    maxResEnabled: boolean;
    maxHeight: string,
    convertToMP3: boolean,
    isGettingPlaylist: boolean
  };
}

export default Vue.extend({
  methods: {
    ChooseDirectory() {
      try {
        const folder = this.$q.electron.remote.dialog.showOpenDialog({
          properties: ['openDirectory']
        })[0];
        this.$store.commit('index/setFolder', folder);
      } catch (obj) {
        console.error(obj);
      }
    },
    async getPlaylistInfo() {
      this.IsGettingInfo = true;
      try {
        this.info.currentUrl = this.url;
        const data: VideoPlaylist = await this.$store.dispatch(
          'mediamanager/getPlaylistInfo',
          this.url
        );
        if (data.extractor.includes('youtube')) {
          data.entries.forEach(entry => {
            entry.url = `https://www.youtube.com/watch?v=${entry.url}`;
          });
        }
        console.log(data);

        this.info.playlistData = data;
      } catch (error) {
        this.$q.notify({
              message: 'Could not get playlist',
              color: 'negative',
              timeout: 1000
          });
        console.error(error);
      } finally {
        this.IsGettingInfo = false;
      }
    },
    async downloadPlaylist() {
      const baseFormats = this.info.baseFormat.split('+');
      this.info.isGettingPlaylist = true;
      try{
        if(this.info.maxResEnabled){
            if(!this.info.maxHeight){
                throw new Error("Max height not set")
            }
            const videoIndex = baseFormats.findIndex(format => format === 'best' || format.includes('video'));
            if(videoIndex !== -1){
              baseFormats[videoIndex] += `[height<=${this.info.maxHeight}]`;
            }
            
        }
        if(!this.info.playlistData){
            throw new Error("No playlist");
        }
        if(!this.$store.getters['index/folder']){
            throw new Error("Please choose a folder");
        }
        
        const playlistTask:PlaylistTask = {
            playlist: this.info.playlistData,
            format: baseFormats.join('+'),
            folder: this.$store.getters['index/folder'],
            title: this.info.playlistData.title,
            id: this.info.playlistData.id,
            ext: baseFormats.length > 1 ? 'mp4' : baseFormats[0] === "bestaudio" && this.info.convertToMP3 ? 'mp3' : ''
        }
        await this.$store.dispatch('playlist/addPlaylistItems', playlistTask);
        this.info.playlistData = null;
        this.url = "";
      }
      catch(err){
            this.$q.notify({
              message: err.message,
              color: 'negative',
              timeout: 1000
          });
      }
      finally{
        this.info.isGettingPlaylist = false;
      }

    },
    containsVideo(format:string){
      const baseFormats = format.split('+');
      return baseFormats.some(format => format === 'best' || format.includes('video'));
    }
  },
  data() {
    const data: Playlist = {
      IsGettingInfo: false,
      url: '',
      info: {
        currentUrl: '',
        playlistData: null,
        listExpanded: false,
        convertToMP3: false,
        columns: [
          {
            field: 'title',
            header: 'Title'
          }
        ],
        isGettingPlaylist: false,
        baseFormat: 'best',
        maxResEnabled: false,
        maxHeight:'1080'
      }
    };
    return data;
  }
});
</script>

<style lang="scss">
.playlist-info {
  display: flex;
  flex-direction: column;
}
.playlist {
  max-height: 16rem;
  .playlist-header {
    .playlist-head {
      text-align: left;
    }
  }
}
</style>
