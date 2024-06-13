// https://jaybharadia.hashnode.dev/how-to-integrate-vue-toastification-in-nuxt-3
import Toast, { type PluginOptions } from "vue-toastification";
import "vue-toastification/dist/index.css";

export default defineNuxtPlugin((nuxtApp) => {
    nuxtApp.vueApp.use(Toast, {
        timeout: 1000,
    } satisfies PluginOptions);
});
