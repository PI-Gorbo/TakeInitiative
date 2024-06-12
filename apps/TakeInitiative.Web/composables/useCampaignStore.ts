import { useCampaignCombatsStore } from "./useCampaignCombatsStore";
import type {
    CampaignMemberDto,
    CombatDto,
    GetCampaignResponse,
} from "base/utils/api/campaign/getCampaignRequest";
import type {
    Campaign,
    CampaignMemberInfo,
    PlannedCombat,
    PlayerCharacter,
} from "./../utils/types/models";
import type { CampaignMember } from "../utils/types/models";
import type { UpdateCampaignDetailsRequest } from "base/utils/api/campaign/updateCampaignDetailsRequest";
import type { CreatePlannedCombatRequest } from "base/utils/api/plannedCombat/createPlannedCombatRequest";
import type { PlayerCharacterDto } from "base/utils/api/campaign/createPlayerCharacterRequest";

export const useCampaignStore = defineStore("campaignStore", () => {
    const api = useApi();
    const userStore = useUserStore();
    const plannedCombatStore = useCampaignCombatsStore();

    const state = reactive<Partial<GetCampaignResponse>>({
        campaign: undefined,
        combatHistoryInfo: undefined,
        currentCombatInfo: undefined,
        joinCode: undefined,
        nonUserCampaignMembers: undefined,
        userCampaignMember: undefined,
    });

    async function fetchCampaign(
        campaignId: string,
    ): Promise<GetCampaignResponse> {
        console.log("refetching campaign info");
        return await api.campaign.get({ campaignId });
    }

    const setCampaignById = async (campaignId: string): Promise<void> => {
        return fetchCampaign(campaignId).then(setCampaign);
    };

    async function setCampaign(
        campaignDetails: GetCampaignResponse,
    ): Promise<void> {
        Object.keys(campaignDetails).forEach((key) => {
            // @ts-ignore
            state[key] = campaignDetails[key];
        });
    }

    async function updateCampaignDetails(
        request: Omit<UpdateCampaignDetailsRequest, "campaignId">,
    ) {
        return await api.campaign
            .update({
                ...request,
                campaignId: state.campaign?.id!,
            })
            .then((campaign) => {
                state.campaign = campaign;
            })
            .then(userStore.refetchUser);
    }

    // Member Details
    const memberDtos: ComputedRef<CampaignMemberDto[]> = computed(() => {
        if (
            state.nonUserCampaignMembers == undefined ||
            state.userCampaignMember == undefined
        ) {
            return [];
        }

        return [
            ...state.nonUserCampaignMembers,
            {
                userId: state.userCampaignMember.userId,
                isDungeonMaster: state.userCampaignMember.isDungeonMaster,
                username: userStore.state.user?.username!,
            },
        ];
    });

    async function openCombat(plannedCombatId: string) {
        return api.combat
            .open({ plannedCombatId: plannedCombatId })
            .then(() => setCampaignById(state.campaign?.id!));
    }

    // Player Character Management //
    async function createPlayerCharacter(dto: PlayerCharacterDto) {
        return await api.campaign.playerCharacters
            .create({
                campaignMemberId: state.userCampaignMember?.id!,
                playerCharacter: dto,
            })
            .then((member) => (state.userCampaignMember = member));
    }

    async function updatePlayerCharacter(
        characterId: string,
        dto: PlayerCharacterDto,
    ) {
        return await api.campaign.playerCharacters
            .update({
                campaignMemberId: state.userCampaignMember?.id!,
                playerCharacterId: characterId,
                playerCharacter: dto,
            })
            .then((member) => (state.userCampaignMember = member));
    }

    async function deletePlayerCharacter(characterId: string) {
        return await api.campaign.playerCharacters
            .delete({
                memberId: state.userCampaignMember?.id!,
                playerCharacterId: characterId,
            })
            .then((member) => (state.userCampaignMember = member));
    }

    return {
        state,
        refetchCampaign: async () => await setCampaignById(state.campaign?.id!),
        setCampaign,
        setCampaignById,
        updateCampaignDetails,
        openCombat,
        createPlayerCharacter,
        updatePlayerCharacter,
        deletePlayerCharacter,
        isDm: computed(
            () => state.userCampaignMember?.isDungeonMaster ?? false,
        ),
        memberDtos,
        getMemberDetailsFor: (id: string): CampaignMemberDto | undefined =>
            memberDtos.value.find((x) => x.userId == id),
    };
});
