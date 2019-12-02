<template>
    <div class="q-pa-md">
        <div class="q-gutter-md">
            <q-btn
                push
                color="primary"
                label="Remove finished"
                @click="clearFinished"
            />
            <q-btn
                push
                color="primary"
                label="Remove error"
                @click="clearErrored"
            />
            <q-btn
                push
                color="primary"
                label="Start All"
                @click="startAll"
            />
            <q-btn
                push
                color="primary"
                label="Stop All"
                @click="stopAll"
            />

            <q-card>
                <q-list bordered padding separator>
                    <q-item
                        v-for="(task, index) in $store.getters[
                            'mediamanager/getTaskQueue'
                        ]"
                        :key="`${index}-task`"
                    >
                        <q-item-section avatar>
                            <q-avatar>
                                <img
                                    :src="`${task.info.videoInfo.thumbnail}`"
                                />
                            </q-avatar>
                        </q-item-section>
                        <q-item-section>
                            <q-item-label overline>{{
                                task.info.videoInfo.fulltitle
                            }}</q-item-label>
                            <q-item-label>
                                <q-linear-progress
                                    v-if="!task.inProgress"
                                    :value="0"
                                />
                                <q-linear-progress
                                    v-else-if="task.percentage == 404"
                                    indeterminate
                                />
                                <q-linear-progress
                                    v-else-if="task.percentage > 0"
                                    :value="task.percentage"
                                />

                                <q-linear-progress
                                    v-else-if="task.inProgress"
                                    query
                                />
                                <q-linear-progress v-else :value="0" />
                            </q-item-label>
                            <q-item-label caption>{{
                                task.status.toUpperCase()
                            }}</q-item-label>
                        </q-item-section>
                        <q-item-section top side >
                            <div class="text-grey-8 q-gutter-xs">
                            <!-- STOP BUTTON -->
                            <q-btn
                                v-if="task.inProgress"
                                flat
                                round
                                size="12px"
                                color="primary"
                                icon="stop"
                                class="inline"
                                @click="stopTask(task.info.id)"
                            />
                            <!-- START BUTTON -->
                            <q-btn
                                v-else-if="task.inProgress === false"
                                flat
                                round
                                size="12px"
                                color="primary"
                                icon="play_arrow"
                                class="inline"
                                @click="startTask(task.info.id)"
                            />

                            <!-- DELETE BUTTON -->
                            <q-btn
                                v-if="task.inProgress"
                                disable
                                flat
                                round
                                size="12px"
                                color="primary"
                                icon="delete"
                            />
                            <q-btn
                                v-else
                                flat
                                round
                                size="12px"
                                color="primary"
                                icon="delete"
                                @click="clearTask(task)"
                            />
                            </div>
                        </q-item-section>
                        <q-item-section side top>
                            <q-item-label caption>{{task.info.chosenFormat.ext}}</q-item-label>
                        </q-item-section>
                    </q-item>
                </q-list>
            </q-card>
        </div>
    </div>
</template>

<script lang="ts">
import Vue from 'vue';
import { VideoTask } from '../store/Mediamanager/mediaManager';

export default Vue.extend({
    methods: {
        clearTask(task:VideoTask) {
            this.$store.dispatch('mediamanager/removeTask', task);
        },
        clearFinished() {
            this.$store.dispatch('mediamanager/removeAllTasksWithStatus', {status: 'done'});
        },
        clearErrored() {
            this.$store.dispatch('mediamanager/removeAllTasksWithStatus', {status: 'error'});
        },
        stopTask(id: string) {
            this.$store.dispatch('mediamanager/stopTask', id);
        },
        startTask(id: string) {
            this.$store.dispatch('mediamanager/startTask', id);
        },
        startAll(){
            this.$store.dispatch('mediamanager/startAllTasks');
        },
        stopAll(){
            this.$store.dispatch('mediamanager/stopAllTasks');
        }
    }
});
</script>

<style></style>
