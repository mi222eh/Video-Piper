<template>
    <div class="q-pa-md">
        <!--DIRECTORY INPUT-->
        <q-item clickable v-ripple @click="ChooseDirectory">
            <q-item-section avatar top>
                <q-avatar icon="folder" color="primary" text-color="white" />
            </q-item-section>
            <q-item-section>
                <q-item-label>Output Directory</q-item-label>
                <q-item-label caption>{{
                    $store.getters['index/folder']
                }}</q-item-label>
            </q-item-section>
        </q-item>
        <!--URL INPUT-->
        <q-input stack-label v-model="url" label="Playlist URL" />
        <!--GET INFO BUTTON-->
        <q-btn
            :loading="IsGettingInfo"
            class="full-width q-mt-md"
            color="primary"
            label="Get Info"
            @click="getPlaylistInfo"
        />
    </div>
</template>

<script lang="ts">
import Vue from 'vue';

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
            const data = await this.$store.dispatch(
                'mediamanager/getPlaylistInfo',
                this.url
            );
            console.log(data);
            this.IsGettingInfo = false;
        }
    },
    data() {
        return {
            IsGettingInfo: false,
            url: '',
            playlistData: {}
        };
    }
});
</script>

<style></style>
