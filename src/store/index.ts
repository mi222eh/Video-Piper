// @ts-nocheck
import Vue from 'vue';
import Vuex from 'vuex';
/**
 * @callback mutations
 * @param {String} type
 *
 */

import mediamanager from './Mediamanager';
import index from './index/index';

Vue.use(Vuex);
export default function () {
    const Store = new Vuex.Store({
        modules: {
            mediamanager,
            index
        },
        strict: !!process.env.DEV
    });
    Store.dispatch('mediamanager/init');
    return Store;
}
