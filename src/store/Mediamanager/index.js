import state from './state';
import * as getters from './getters';
import * as mutations from './mutations';
import * as actions from './actions';
import './doc/MediaManagerDoc';

export default {
    namespaced: true,
    state,
    getters,
    mutations,
    actions
};
