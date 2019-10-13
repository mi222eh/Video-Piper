<template>
  <q-layout view="hHh LpR fFf">

    <q-header reveal elevated class="bg-primary text-white" height-hint="98">
      <q-toolbar>
        <q-toolbar-title>
          <q-avatar>
            <img src="~assets/app-icon.png">
          </q-avatar>
          Video Piper
        </q-toolbar-title>
      </q-toolbar>

      <q-tabs align="left">
        <q-route-tab to="/" :label="`Home`" />
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

<script>
import { join } from 'path';
export default {
    methods: {
        getAppLogo: function () {
            const path = join(__statics, 'app-icon.png');
            console.log(path);

            return path;
        }
    },
    data () {
        return {
        };
    }
};
</script>

<style>
body {
  overflow: auto;
  background:
  linear-gradient(45deg,rgb(9, 10, 58), transparent),
  repeating-linear-gradient(45deg, rgb(21, 102, 194) 0%, rgb(99, 147, 201) 5%, transparent 5%, transparent 10%),
  rgb(0, 247, 255);
}
</style>
