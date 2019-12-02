import { RouteConfig } from 'vue-router'
const routes: RouteConfig[] = [
    {
        path: '/',
        component: () => import('layouts/MyLayout.vue'),
        children: [
            { path: '', component: () => import('pages/Index.vue') },
            { path: '/queue', component: () => import('pages/Queue.vue') },
            { path: '/playlist', component: () => import('pages/Playlist.vue') }
        ]
    }
];

// Always leave this as last one
if (process.env.MODE !== 'ssr') {
    routes.push({
        path: '*',
        component: () => import('pages/Error404.vue')
    });
}

export default routes;
