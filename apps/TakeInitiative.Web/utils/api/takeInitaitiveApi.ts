import { createCampaignRequest } from "./campaign/createCampaignRequest";
import { getCampaignRequest } from "./campaign/getCampaignRequest";
import { joinCampaignRequest } from "./campaign/joinCampaignRequest";
import { updateCampaignDetailsRequest } from "./campaign/updateCampaignDetailsRequest";
import { getUserRequest } from "./user/getUserRequest";
import { signUpRequest } from "./user/signUpRequest";

export const useTakeInitApi = () => {
    const { $axios } = useNuxtApp();
    return {
        user: {
            getUser: getUserRequest($axios),
            signUp: signUpRequest($axios),
        },
        campaign: {
            create: createCampaignRequest($axios),
            join: joinCampaignRequest($axios),
            update: updateCampaignDetailsRequest($axios),
			get: getCampaignRequest($axios)
        },
    };
};
