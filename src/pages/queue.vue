<template>
  <div class="q-pa-md">
    <div class="q-gutter-md">
        <q-card>
            <q-list bordered padding>
                <q-item clickable v-ripple v-for="(task, index) in $store.getters['mediamanager/getTaskQueue']" :key="`${index}-task`">
                    <q-item-section avatar>
                        <q-avatar>
                            <img :src="`${task.thumbnail}`">
                        </q-avatar>
                    </q-item-section>
                    <q-item-section>
                        <q-item-label overline>{{task.title}}</q-item-label>
                        <q-item-label><q-linear-progress style="height: 25px" :value="task.percentage" stripe class="q-mt-sm" /></q-item-label>
                        <q-item-label caption>{{task.status.toUpperCase()}}</q-item-label>
                    </q-item-section>
                    <q-item-section side top>
                        <q-item-label caption>{{task.chosenExtension}}</q-item-label>
                    </q-item-section>
                </q-item>
            </q-list>
        </q-card>
    </div>
  </div>
</template>

<script>
export default {

};
</script>

<style>

</style>
