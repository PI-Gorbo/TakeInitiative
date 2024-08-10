import type { AxiosError, AxiosInstance, CreateAxiosDefaults } from "axios";
import { Api } from "base/utils/api/api";

export default defineNuxtPlugin(async (nuxtApp) => {
    // Destructure the environment variables to get axios config
    const {
        public: { axios: axiosConfig },
    } = useRuntimeConfig();

    const api = new Api({ ...axiosConfig });

    // Register to use an auth token if there is one.
    const aspNetCoreCookie = useCookie(".AspNetCore.Cookies"); // Axios does not attach the cookie on the first request. so we have to manually do it.
    api.instance.interceptors.request.use((config) => {
        if (aspNetCoreCookie.value) {
            config.headers["Cookie"] =
                `.AspNetCore.Cookies=${aspNetCoreCookie.value}`;
        }
        config.withCredentials = true; // Automatically adds cookies to every request.
        return config;
    });

    api.instance.interceptors.response.use(
        (resp) => {
            return resp;
        },
        async (error) => {
            if (error?.response?.status == 401) {
                const axiosError: AxiosError = error as AxiosError;
                await navigateTo("/login");
                throw error;
            }

            throw error;
        },
    );

    return {
        provide: {
            axios: api.instance,
            api,
        },
    };
});
