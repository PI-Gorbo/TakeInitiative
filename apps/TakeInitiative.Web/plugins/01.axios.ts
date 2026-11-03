import type { AxiosInstance, CreateAxiosDefaults } from "axios";
import axios from "axios";
import { authCookies } from "~/utils/session";
export default defineNuxtPlugin((nuxtApp) => {
    // Destructure the environment variables to get axios config
    const {
        public: { axios: axiosConfig },
    } = useRuntimeConfig();

    const routeInfo = useRoute();

    // Register axios
    const defaultAxios: CreateAxiosDefaults = { ...axiosConfig };
    const Axios: AxiosInstance = axios.create(defaultAxios);

    // During the server render, pass the visitor's auth cookies on to the API: nothing else
    // would. In the browser, `withCredentials` sends them (and a Cookie header can't be set).
    const serverCookies = import.meta.server
        ? authCookies(useRequestHeaders(["cookie"]).cookie).join("; ")
        : "";
    Axios.interceptors.request.use((config) => {
        if (serverCookies) {
            config.headers["Cookie"] = serverCookies;
        }
        config.withCredentials = true;
        return config;
    });

    Axios.interceptors.response.use(
        (resp) => {
            return resp;
        },
        async (error) => {
            if (error?.response?.status == 401 && routeInfo.meta.requiresAuth) {
                await goToSignIn(useRoute().fullPath);
                throw error;
            }

            throw error;
        }
    );

    return {
        provide: {
            axios: Axios,
        },
    };
});
