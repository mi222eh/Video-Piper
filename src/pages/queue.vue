<template>
  <div class="q-pa-md">
    <div class="q-gutter-md">
        <q-btn push color="primary" label="Clear finished" @click="clearFinished"/>
        <q-card>
            <q-list bordered padding separator>
                <q-item v-for="(task, index) in $store.getters['mediamanager/getTaskQueue']" :key="`${index}-task`">
                    <q-item-section avatar>
                        <q-avatar>
                            <img :src="`${task.thumbnail}`">
                        </q-avatar>
                    </q-item-section>
                    <q-item-section>
                        <q-item-label overline>{{task.title}}</q-item-label>
                        <q-item-label>
                            <q-linear-progress v-if="task.status == 'preparing'" query />
                            <q-linear-progress v-else-if="task.percentage == 404" indeterminate />
                            <q-linear-progress v-else :value="task.percentage"/>
                        </q-item-label>
                        <q-item-label caption>{{task.status.toUpperCase()}}</q-item-label>
                    </q-item-section>
                        <q-item-section>
                            <q-btn v-if="task.status !== 'done'" disable flat round color="primary" icon="delete" />
                            <q-btn v-else flat round color="primary" icon="delete" @click="clearTask(task.id)"/>
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
import '../store/Mediamanager/doc/MediaManagerDoc';
export default {
    methods: {
        /**
         * @function clearTask
         * @param  {Number} id
         */
        clearTask: function (id) {
            this.$store.commit('mediamanager/clearTask', id);
        },
        clearFinished: function () {
            this.$store.commit('mediamanager/clearFinishedTasks');
        },
        abortTask: function (id) {
            this.$store.dispatch('mediamanager/abort', id);
        }
    }
};
</script>

<style>

</style>
