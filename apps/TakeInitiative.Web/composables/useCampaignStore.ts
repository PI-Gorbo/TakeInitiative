import { usePlannedCombatStore } from "./usePlannedCombatStore";
import type {
    CampaignMemberDto,
    CombatDto,
    GetCampaignResponse,
} from "~/utils/api/campaign/getCampaignRequest";
import type {
    Campaign,
    CampaignMemberInfo,
    PlannedCombat,
    PlayerCharacter,
} from "./../utils/types/models";
import type { CampaignMember } from "../utils/types/models";
import type { UpdateCampaignDetailsRequest } from "~/utils/api/campaign/updateCampaignDetailsRequest";
import type { CreatePlannedCombatRequest } from "~/utils/api/plannedCombat/createPlannedCombatRequest";

export const useCampaignStore = defineStore("campaignStore", () => {
    const api = useApi();
    const userStore = useUserStore();
    const plannedCombatStore = usePlannedCombatStore();

    const state = reactive<Partial<GetCampaignResponse>>({
        campaign: undefined,
        combatDto: undefined,
        joinCode: undefined,
        userCampaignMember: undefined,
        finishedCombats: undefined,
        nonUserCampaignMembers: undefined,
        plannedCombats: undefined,
    });

    async function init(): Promise<void> {
        return await userStore.isLoggedIn().then(async (loggedIn) => {
            if (!loggedIn) {
                return Promise.reject("User is not logged in");
            }

            // Initialize Dependencies.
            if (userStore.state.selectedCampaignId == null) {
                return Promise.reject(
                    "User store is empty. Cannot initialize the campaign store.",
                );
            }

            return await setCampaignById(userStore.state.selectedCampaignId);
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
        Object.keys(campaignDetails)
            .forEach((key) => {
                state[key] = campaignDetails[key]
            })
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
            });
    }

    // Planned Combats
    async function createPlannedCombat(
        request: Omit<CreatePlannedCombatRequest, "campaignId">,
    ): Promise<PlannedCombat> {
        return await api.plannedCombat
            .create({
                ...request,
                campaignId: state.campaign?.id!,
            })
            .then((combat) => {
                state.campaign?.plannedCombatIds?.push(combat.id);
                state.plannedCombats?.push(combat);
                return combat;
            });
    }

    async function setPlannedCombat(combatId: string) {
        plannedCombatStore.setPlannedCombat(
            state.plannedCombats?.find((x) => x.id == combatId),
        );
    }

    async function deletePlannedCombat(plannedCombatId: string) {
        return await api.plannedCombat
            .delete({
                campaignId: state.campaign!.id,
                combatId: plannedCombatId,
            })
            .then(() => {
                state.plannedCombats = state.plannedCombats?.filter(
                    (x) => x.id != plannedCombatId,
                );

                if (state.campaign?.plannedCombatIds) {
                    state.campaign.plannedCombatIds =
                        state.campaign.plannedCombatIds?.filter(
                            (x) => x != plannedCombatId,
                        );
                }

                // If the plannedCombatStore has the planned combat as the selected combat, then
                // change it.
                if (
                    plannedCombatStore.selectedPlannedCombat?.id ==
                    plannedCombatId
                ) {
                    if ((state.plannedCombats?.length ?? 0) > 0) {
                        plannedCombatStore.setPlannedCombat(
                            state.plannedCombats![0],
                        );
                    }
                } else {
                    plannedCombatStore.setPlannedCombat(null);
                }
            });
    }

    // Member Details
    const memberDtos: ComputedRef<CampaignMemberDto[]> = computed(() => {
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
        createPlannedCombat,
        setPlannedCombat,
        deletePlannedCombat,
        isDm: computed(
            () => state.userCampaignMember?.isDungeonMaster ?? false,
        ),
        memberDtos,
        getMemberDetailsFor: (id: string): CampaignMemberDto | undefined =>
            memberDtos.value.find((x) => x.userId == id),
    };
});
