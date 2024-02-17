import type {
    CampaignMemberDto,
    GetCampaignResponse,
} from "~/utils/api/campaign/getCampaignRequest";
import type {
    Campaign,
    PlannedCombat,
    PlayerCharacter,
} from "./../utils/types/models";
import type { CampaignMember } from "../utils/types/models";
import type { UpdateCampaignDetailsRequest } from "~/utils/api/campaign/updateCampaignDetailsRequest";

export const useCampaignStore = defineStore("campaignStore", () => {
    const api = useApi();
    const userStore = useUserStore();
    const state = reactive({
        campaign: undefined as Campaign | undefined,
        userCampaignMember: undefined as CampaignMember | undefined,
        nonUserCampaignMembers: undefined as CampaignMemberDto[] | undefined,
        plannedCombats: undefined as PlannedCombat[] | undefined,
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

            return await setCampaignById(campaignId);
        });
    }

    async function fetchCampaign(
        campaignId: string,
    ): Promise<GetCampaignResponse> {
        return await api.campaign.get({ campaignId });
    }

    const setCampaignById = async (campaignId: string): Promise<void> =>
        fetchCampaign(campaignId).then(setCampaign);

    async function setCampaign(
        campaignDetails: GetCampaignResponse,
    ): Promise<void> {
        state.campaign = campaignDetails.campaign;
        state.nonUserCampaignMembers = campaignDetails.nonUserCampaignMembers;
        state.plannedCombats = campaignDetails.plannedCombats;
        state.userCampaignMember = campaignDetails.userCampaignMember;
    }

    async function updateCampaignDetails(
        request: Omit<UpdateCampaignDetailsRequest, "campaignId">,
    ) {
        return await api.campaign
            .update({
                ...request,
                campaignId: state.campaign.id,
            })
            .then((campaign) => {
                state.campaign = campaign;
            });
    }

    const memberDtos = computed(() => {
        if (
            state.nonUserCampaignMembers == undefined ||
            state.userCampaignMember == undefined
        ) {
            return [];
        }

        const currentUserCurrentCharacter =
            state.userCampaignMember.currentCharacterId != null
                ? state.userCampaignMember.characters?.find(
                      (x) =>
                          x.id == state.userCampaignMember?.currentCharacterId,
                  )
                : null;

        return [
            ...state.nonUserCampaignMembers,
            {
                userId: state.userCampaignMember.userId,
                isDungeonMaster: state.userCampaignMember.isDungeonMaster,
                currentCharacter:
                    currentUserCurrentCharacter as PlayerCharacter | null,
                username: userStore.state.user?.username!,
            },
        ];
    });

    return {
        state,
        init,
        setCampaign,
        setCampaignById,
        updateCampaignDetails,
        isDm: computed(
            () => state.userCampaignMember?.isDungeonMaster ?? false,
        ),
        memberDtos,
    };
});
