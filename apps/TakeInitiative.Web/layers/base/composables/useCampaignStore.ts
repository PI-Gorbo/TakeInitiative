import type {
    CampaignMemberDto,
    CampaignMemberResource,
    GetCampaignResponse,
    PlayerCharacterDTO,
    PutCampaignDetailsRequest,
} from "base/utils/api/api";
import { useCampaignCombatsStore } from "./useCampaignCombatsStore";
import * as signalR from "@microsoft/signalr";

export const useCampaignStore = defineStore("campaignStore", () => {
    const api = useApi();
    const userStore = useUserStore();
    const plannedCombatStore = useCampaignCombatsStore();

    // Signal R
    // Start the connection.
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(`${useRuntimeConfig().public.axios.baseURL}/campaignHub`, {
            accessTokenFactory: () => useCookie(".AspNetCore.Cookies").value!,
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Debug)
        .build();
    connection.on("campaignStateUpdated", async () => {
        await refetchCampaign();
        return;
    });
    connection.on("campaignMemberStateUpdated", async () => {
        await refetchCampaign();
        return;
    });
    connection.onreconnected(async () => {
        // Join the campaign hub.
        await refetchCampaign();
        await connection.send("Join", state.campaign?.id); // rejoin as you leave all groups on disconnect.
    });

    async function joinCampaignHub(id: string) {
        return await connection.start().then(
            async () =>
                // Join the campaign hub.
                await connection.send("Join", id),
        );
    }
    async function leaveCampaignHub() {
        return await connection.send("Leave", state.campaign?.id);
    }

    // State
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
        return await api.campaign.get({ campaignId });
    }

    const setCampaignById = async (campaignId: string): Promise<void> => {
        return fetchCampaign(campaignId).then(setCampaign);
    };

    const refetchCampaign = () => setCampaignById(state.campaign?.id!);

    async function setCampaign(
        campaignDetails: GetCampaignResponse,
    ): Promise<void> {
        if (
            state.campaign?.id != undefined &&
            connection.state == signalR.HubConnectionState.Connected &&
            state.campaign?.id != campaignDetails.campaign?.id
        ) {
            await connection.send("Leave", state.campaign?.id);
        }

        Object.keys(campaignDetails).forEach((key) => {
            // @ts-ignore
            state[key] = campaignDetails[key];
        });
    }

    async function updateCampaignDetails(
        request: Omit<PutCampaignDetailsRequest, "campaignId">,
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
    async function createPlayerCharacter(dto: PlayerCharacterDTO) {
        return await api.campaign.playerCharacters
            .create({
                campaignMemberId: state.userCampaignMember?.id!,
                playerCharacter: dto,
            })
            .then((member) => (state.userCampaignMember = member));
    }

    async function updatePlayerCharacter(
        characterId: string,
        dto: PlayerCharacterDTO,
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

    async function setCampaignMemberResources(
        resources: CampaignMemberResource[],
    ) {
        return await api.campaign.member
            .setResources({
                memberId: state.userCampaignMember?.id!,
                resources: resources,
            })
            .then(refetchCampaign);
    }

    return {
        state,
        refetchCampaign,
        setCampaign,
        setCampaignById,
        updateCampaignDetails,
        openCombat,
        createPlayerCharacter,
        updatePlayerCharacter,
        deletePlayerCharacter,
        setCampaignMemberResources,
        joinCampaignHub,
        leaveCampaignHub,
        isDm: computed(
            () => state.userCampaignMember?.isDungeonMaster ?? false,
        ),
        memberDtos,
        getMemberDetailsFor: (id: string): CampaignMemberDto | undefined =>
            memberDtos.value.find((x) => x.userId == id),
        memberResources: computed(() => {
            return [
                {
                    userId: state.userCampaignMember?.userId,
                    username: userStore.state.user?.username!,
                    resources: state.userCampaignMember?.resources?.sort(
                        (a, b) =>
                            multiplePropertyAlphabeticalSort(
                                a,
                                b,
                                (ob) => ob.link ?? "",
                                (ob) => ob.name ?? "",
                            ),
                    ),
                    isDm: state.userCampaignMember?.isDungeonMaster,
                },
                ...(state.nonUserCampaignMembers ?? []).map((member) => ({
                    userId: member.userId,
                    username: member.username!,
                    resources: member.resources?.sort((a, b) =>
                        multiplePropertyAlphabeticalSort(
                            a,
                            b,
                            (ob) => ob.link ?? "",
                            (ob) => ob.name ?? "",
                        ),
                    ),
                    isDm: member.isDungeonMaster,
                })),
            ].sort((member1, member2) => {
                if (member1.isDm && !member2.isDm) {
                    // Sort by is Dm
                    return -1;
                } else if (!member1.isDm && member2.isDm) {
                    return 1;
                }

                // Sort by username.
                return member1.username > member2.username ? 1 : -1;
            });
        }),
    };
});

const multiplePropertyAlphabeticalSort = <T>(
    a: T,
    b: T,
    ...args: Array<(obj: T) => string>
): 1 | 0 | -1 => {
    for (let arg of args) {
        const comparisonResult = alphabeticalSort(arg(a), arg(b));
        if (comparisonResult != 0) {
            return comparisonResult;
        }
    }

    return 0;
};
const alphabeticalSort = (a: string, b: string): 1 | 0 | -1 => {
    if (a > b) return 1;
    if (b > a) return -1;
    return 0;
};
