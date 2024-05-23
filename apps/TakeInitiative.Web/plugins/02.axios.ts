import type { AxiosError, AxiosInstance, CreateAxiosDefaults } from "axios";
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

    console.log("Adding request interceptor...")
    const aspNetCoreCookie = useCookie(".AspNetCore.Cookies"); // Axios does not attach the cookie on the first request. so we have to manually do it.
    Axios.interceptors.request.use((config) => {
        console.log("Request interceptor start", aspNetCoreCookie.value)
        console.log("Attempting with the get cookie request")
        if (aspNetCoreCookie.value) {
            config.headers["Cookie"] =
                `.AspNetCore.Cookies=${aspNetCoreCookie.value}`;
        }
        config.withCredentials = true; // Automatically adds cookies to every request.
        return config;
    });
    console.log("Finished Adding request interceptor")

    Axios.interceptors.response.use(
        (resp) => {
            return resp;
        },
        async (error) => {
            if (error.response.status == 401) {
                const axiosError: AxiosError = error as AxiosError
                console.error("Unauthenticated. Redirecting to /login")
                console.error(axiosError?.request);
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
