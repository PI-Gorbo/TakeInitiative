import { user } from "./user";
import { campaign } from "./campaign";

export const useTakeInitApi = () => {
    const { $axios } = useNuxtApp();
    return {
        user: user($axios),
        campaign: campaign($axios),
    };
};
