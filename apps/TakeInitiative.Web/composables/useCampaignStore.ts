import type { Campaign } from "./../utils/types/models";

export const useCampaignStore = defineStore("campaignStore", () => {
    const api = useApi();
    const userStore = useUserStore();
    const state = reactive({
        campaign: null as Campaign | null,
        isDm: false,
    });

    async function init(): Promise<void> {
        return await userStore.isLoggedIn().then(async (loggedIn) => {
            if (!loggedIn) {
                return Promise.reject("User is not logged in");
            }

            // Initialize Dependencies.
            const memberDetails = userStore.state.user?.dmCampaigns.concat(
                userStore.state.user.memberCampaigns,
            )[0];
            const campaignId: string | undefined =
                state.campaign?.id ?? memberDetails?.campaignId;

            if (campaignId == null) {
                return Promise.reject(
                    "User store is empty. Cannot initialize the campaign store.",
                );
            }

            return await setCampaignById(campaignId).then(() => {
                state.isDm =
                    state.campaign?.campaignMemberInfo?.find(
                        (x) => x.userId == userStore.state.user?.userId,
                    )?.isDungeonMaster ?? false;
            });
        });
    }

    async function fetchCampaign(campaignId: string): Promise<Campaign> {
        return await api.campaign.get({ campaignId });
    }

    const setCampaignById = async (campaignId: string): Promise<void> =>
        fetchCampaign(campaignId).then(setCampaign);
    async function setCampaign(campaign: Campaign): Promise<void> {
        state.campaign = campaign;
    }

    return {
        state,
        init,
        setCampaign,
        setCampaignById,
    };
});
