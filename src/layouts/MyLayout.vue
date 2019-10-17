<template>
  <q-layout view="hHh lpR lfr">
    <q-header elevated class="bg-primary text-white">
      <q-toolbar>
        <q-toolbar-title class="text-h2">Video Piper</q-toolbar-title>
      </q-toolbar>
    </q-header>

    <q-page-container>
      <router-view />
    </q-page-container>
  </q-layout>
</template>

<script>
export default {
    data () {
        return {};
    }
};
</script>

<style>
body {
  overflow: auto;
}
</style>
