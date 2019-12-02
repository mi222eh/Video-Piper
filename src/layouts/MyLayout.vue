<template>
  <q-layout view="hHh LpR fFf">

    <q-header elevated class="bg-primary text-white" height-hint="98">
      <q-tabs align="left">
        <q-route-tab to="/" :label="`Video`" />
        <!-- <q-route-tab to="/playlist" :label="`Playlist`" /> -->
        <q-route-tab to="/queue" :label="`Video Queue`">
            <q-badge color="accent" text-color="white" floating>{{$store.getters['mediamanager/getActiveQueueCount']}}</q-badge>
        </q-route-tab>
      </q-tabs>
    </q-header>

    <q-page-container>
      <router-view />
    </q-page-container>

  </q-layout>
</template>

<script lang="ts">
import Vue from 'vue'

export default Vue.extend({
    data () {
        return {
        };
    }
});
</script>

<style>
</style>
