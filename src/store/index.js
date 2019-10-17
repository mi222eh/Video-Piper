import Vue from 'vue';
import Vuex from 'vuex';
/**
 * @callback mutations
 * @param {String} type
 *
 */

import mediamanager from './Mediamanager';

Vue.use(Vuex);
export default function () {
    const Store = new Vuex.Store({
        modules: {
            mediamanager
        },
        strict: process.env.DEV
    });
    // Store.dispatch('mediamanager/init');
    return Store;
}
