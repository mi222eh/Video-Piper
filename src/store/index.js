import Vue from 'vue';
import Vuex from 'vuex';
/**
 * @callback mutations
 * @param {String} type
 *
 */

/**
 * @typedef {{state:(Object | Function), mutations:Function}} VuexContext
 * @property {(Object | Function)} state
 * @property {Function}
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
    Store.dispatch('mediamanager/init');
    return Store;
}
