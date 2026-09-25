import { createCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
import { deleteCampaignRequest } from "~/utils/api/campaign/deleteCampaignRequest";
import { getCampaignRequest } from "~/utils/api/campaign/getCampaignRequest";
import { joinCampaignRequest } from "~/utils/api/campaign/joinCampaignRequest";
import { updateCampaignDetailsRequest } from "~/utils/api/campaign/updateCampaignDetailsRequest";
import { getUserRequest } from "~/utils/api/user/getUserRequest";
import { loginRequest } from "~/utils/api/user/loginRequest";
import { logoutRequest } from "~/utils/api/user/logoutRequest";
import { signUpRequest } from "~/utils/api/user/signUpRequest";
import { postConfirmEmailRequest as confirmEmailRequest } from "~/utils/api/user/postConfirmEmailRequest";
import { postSendConfirmEmailRequest } from "~/utils/api/user/postSendConfirmEmailRequest";
import { putSendResetPasswordRequest } from "~/utils/api/user/putSendResetPasswordRequest";
import { putResetPassword } from "~/utils/api/user/putResetPasswordRequest";
import { getMaintenanceRequest } from "~/utils/api/admin/getMaintainenceRequest";
import { putUsername } from "~/utils/api/user/putUsernameRequest";

export const useApi = () => {
    const { $axios } = useNuxtApp();
    return {
        user: {
            getUser: getUserRequest($axios),
            signUp: signUpRequest($axios),
            login: loginRequest($axios),
            logout: logoutRequest($axios),
            confirmEmailWithToken: confirmEmailRequest($axios),
            sendConfirmationEmail: postSendConfirmEmailRequest($axios),
            sendResetPasswordEmail: putSendResetPasswordRequest($axios),
            resetPasswordWithToken: putResetPassword($axios),
            updateUsername: putUsername($axios)
        },
        campaign: {
            create: createCampaignRequest($axios),
            join: joinCampaignRequest($axios),
            update: updateCampaignDetailsRequest($axios),
            get: getCampaignRequest($axios),
            delete: deleteCampaignRequest($axios),
        },
        admin: {
            getMaintenance: getMaintenanceRequest($axios),
        },
    };
};
