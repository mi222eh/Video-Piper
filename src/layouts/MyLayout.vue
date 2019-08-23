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
  background:
  linear-gradient(45deg,rgb(9, 10, 58), transparent),
  repeating-linear-gradient(45deg, rgb(21, 102, 194) 0%, rgb(99, 147, 201) 5%, transparent 5%, transparent 10%),
  rgb(0, 247, 255);
}
</style>
