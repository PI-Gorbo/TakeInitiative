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

    const aspNetCoreCookie = useCookie(".AspNetCore.Cookies").value; // Axios does not attach the cookie on the first request. so we have to manually do it.
    Axios.interceptors.request.use((config) => {
        if (aspNetCoreCookie) {
            config.headers["Cookie"] =
                `.AspNetCore.Cookies=${aspNetCoreCookie}`;
        }
        config.withCredentials = true; // Automatically adds cookies to every request.
        return config;
    });

    Axios.interceptors.response.use(
        (resp) => {
            return resp;
        },
        async (error) => {
            if (error.response.status == 401) {
                console.log("navigating to /login");
                await navigateTo("/login");
                throw error;
            }

            throw error;
        },
    );

    return {
        provide: {
            axios: Axios,
        },
    };
});
