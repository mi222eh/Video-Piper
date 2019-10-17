<template>
  <q-layout view="hHh LpR fFf">

    <q-header reveal elevated class="bg-primary text-white" height-hint="98">
      <q-toolbar>
        <q-toolbar-title>
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
export default {
    data () {
        return {
        };
    }
};
</script>

<style>
body {
  overflow: auto;
}
</style>
