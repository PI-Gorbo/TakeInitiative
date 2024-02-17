import type { AxiosInstance, CreateAxiosDefaults } from "axios";
import axios from "axios";
export default defineNuxtPlugin((nuxtApp) => {
    // Destructure the environment variables to get axios config
    const {
        public: { axios: axiosConfig },
    } = useRuntimeConfig();

    // Register axios
    const defaultAxios: CreateAxiosDefaults = { ...axiosConfig };
    const Axios: AxiosInstance = axios.create(defaultAxios);
    // Register to use an auth token if there is one.
    Axios.interceptors.request.use((config) => {
        config.withCredentials = true; // Automatically adds cookies to every request.
        return config;
    });

    return {
        provide: {
            axios: Axios,
        },
    };
});
