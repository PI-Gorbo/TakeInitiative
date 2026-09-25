import { createCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
import { getCampaignRequest } from "~/utils/api/campaign/getCampaignRequest";
import { getCampaignsRequest } from "~/utils/api/campaign/getCampaignsRequest";
import { joinCampaignRequest } from "~/utils/api/campaign/joinCampaignRequest";
import { putMemberRoleRequest } from "~/utils/api/campaign/putMemberRoleRequest";
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
import { getSessionsRequest } from "~/utils/api/session/getSessionsRequest";
import { getSessionStreamRequest } from "~/utils/api/session/getSessionStreamRequest";
import { postStartSessionRequest } from "~/utils/api/session/postStartSessionRequest";
import { putSessionTitleRequest } from "~/utils/api/session/putSessionTitleRequest";
import { deleteSessionNoteRequest } from "~/utils/api/sessionNote/deleteSessionNoteRequest";
import { getSessionNoteHistoryRequest } from "~/utils/api/sessionNote/getSessionNoteHistoryRequest";
import { getSessionNoteRequest } from "~/utils/api/sessionNote/getSessionNoteRequest";
import { postSessionNoteRequest } from "~/utils/api/sessionNote/postSessionNoteRequest";
import { putSessionNoteHiddenRequest } from "~/utils/api/sessionNote/putSessionNoteHiddenRequest";
import { putSessionNoteRequest } from "~/utils/api/sessionNote/putSessionNoteRequest";
import { putSessionNoteVisibilityRequest } from "~/utils/api/sessionNote/putSessionNoteVisibilityRequest";
import { getEntriesRequest } from "~/utils/api/entry/getEntriesRequest";
import { getEntryRequest } from "~/utils/api/entry/getEntryRequest";
import { getEntryTimelineRequest } from "~/utils/api/entry/getEntryTimelineRequest";
import { postEntryRequest } from "~/utils/api/entry/postEntryRequest";
import { postEntryQuoteRequest } from "~/utils/api/entry/postEntryQuoteRequest";
import { putEntryAliasesRequest } from "~/utils/api/entry/putEntryAliasesRequest";
import { putEntryArticleRequest } from "~/utils/api/entry/putEntryArticleRequest";
import { putEntryEditAccessRequest } from "~/utils/api/entry/putEntryEditAccessRequest";
import { putEntryKindRequest } from "~/utils/api/entry/putEntryKindRequest";
import { putEntryNameRequest } from "~/utils/api/entry/putEntryNameRequest";
import { putEntryVisibilityRequest } from "~/utils/api/entry/putEntryVisibilityRequest";
import { postEntryMergeRequest } from "~/utils/api/entry/postEntryMergeRequest";
import { putEntryClaimRequest } from "~/utils/api/entry/putEntryClaimRequest";
import { putEntryStatsRequest } from "~/utils/api/entry/putEntryStatsRequest";
import { getEntryHistoryRequest } from "~/utils/api/entry/getEntryHistoryRequest";

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
            get: getCampaignRequest($axios),
            list: getCampaignsRequest($axios),
            putMemberRole: putMemberRoleRequest($axios),
        },
        session: {
            list: getSessionsRequest($axios),
            start: postStartSessionRequest($axios),
            putTitle: putSessionTitleRequest($axios),
            getStream: getSessionStreamRequest($axios),
        },
        note: {
            post: postSessionNoteRequest($axios),
            get: getSessionNoteRequest($axios),
            put: putSessionNoteRequest($axios),
            putVisibility: putSessionNoteVisibilityRequest($axios),
            putHidden: putSessionNoteHiddenRequest($axios),
            delete: deleteSessionNoteRequest($axios),
            history: getSessionNoteHistoryRequest($axios),
        },
        entry: {
            list: getEntriesRequest($axios),
            get: getEntryRequest($axios),
            create: postEntryRequest($axios),
            timeline: getEntryTimelineRequest($axios),
            putName: putEntryNameRequest($axios),
            putKind: putEntryKindRequest($axios),
            putAliases: putEntryAliasesRequest($axios),
            putVisibility: putEntryVisibilityRequest($axios),
            putEditAccess: putEntryEditAccessRequest($axios),
            putArticle: putEntryArticleRequest($axios),
            promote: postEntryQuoteRequest($axios),
            merge: postEntryMergeRequest($axios),
            putClaim: putEntryClaimRequest($axios),
            putStats: putEntryStatsRequest($axios),
            history: getEntryHistoryRequest($axios),
        },
        admin: {
            getMaintenance: getMaintenanceRequest($axios),
        },
    };
};
