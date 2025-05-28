import {
    CombatState,
    type InitiativeCharacter,
    type StagedCharacter,
} from "~/utils/types/models";
import type { CampaignMemberDto } from "~/utils/api/campaign/getCampaignRequest";
import { getCombatQuery } from "~/utils/queries/combats";
import { useQuery } from "@tanstack/vue-query";
import { getCampaignQuery } from "~/utils/queries/campaign";
import { faCircleUser, faCrown, faUserLarge } from "@fortawesome/free-solid-svg-icons";
export type InitiativePlayerDto = {
    user: CampaignMemberDto;
    character: InitiativeCharacter;
};
export type StagedPlayerDto = {
    user: CampaignMemberDto;
    character: StagedCharacter;
};

export const useCombatStore = defineStore("combatStore", () => {

    const userStore = useUserStore()

    // Setup Campaign & Combat Query
    const campaignId = ref<string | null>(null)
    const combatId = ref<string | null>(null)

    function init(newCampaignId: string | null, newCombatId: string | null) {
        campaignId.value = newCampaignId;
        combatId.value = newCombatId;
        campaignQuery.refetch();
        combatQuery.refetch();
    }
    const campaignQuery = useQuery(getCampaignQuery(campaignId));
    const combatQuery = useQuery(getCombatQuery(campaignId, combatId))

    // Query Details
    const isLoading = computed(
        () => campaignQuery.isLoading.value || combatQuery.isLoading.value
    );

    // User Details
    const userIsDm = computed(() => {
        return (
            userStore.state.user?.userId ===
            combatQuery.data.value?.combat?.dungeonMaster
        );
    });

    // Aggregations.
    const memberDtos: ComputedRef<CampaignMemberDto[]> = computed(() => {
        if (isLoading.value) {
            return [];
        }

        const list: CampaignMemberDto[] = [
            ...campaignQuery.data.value?.campaignMembers!,
            {
                ...campaignQuery.data.value!.userCampaignMember,
                username: userStore.state.user?.username!,
            },
        ];

        return list;
    });

    const getMemberDetailsFor = (id: string): CampaignMemberDto | undefined =>
        memberDtos.value.find((x) => x.userId == id);


    // // Start the connection.
    // const connection = new signalR.HubConnectionBuilder()
    //     .withUrl(`${useRuntimeConfig().public.axios.baseURL}/combatHub`, {
    //         accessTokenFactory: () => useCookie(".AspNetCore.Cookies").value!,
    //     })
    //     .withAutomaticReconnect()
    //     .build();

    // connection.onreconnected(async () => {
    //     await setCombat(state.combat?.id!);
    //     await connection.send(
    //         // Rejoin, as users are kicked from all groups on disconnect
    //         "joinCombat",
    //         userStore.state.user?.userId,
    //         state.combat?.id
    //     );
    // });

    // connection.on("combatUpdated", (combat: Combat) => {
    //     state.combat = combat;
    //     return;
    // });






    // async function startConnection() {
    //     if (
    //         connection.state == signalR.HubConnectionState.Connecting ||
    //         connection.state == signalR.HubConnectionState.Connected
    //     ) {
    //         return;
    //     }

    //     await connection.start().catch((error) => {
    //         state.signalRError = error;
    //         throw error;
    //     });
    // }

    // async function updateStagedCharacter(req: StagedCharacterDTO) {
    //     return await api.combat.stage.character.update({
    //         character: req,
    //         combatId: state.combat?.id!,
    //     });
    // }

    // async function addStagedCharacter(req: StagedCharacterWithoutIdDTO) {
    //     return await api.combat.stage.character.add({
    //         character: req,
    //         combatId: state.combat?.id!,
    //     });
    // }

    // async function deleteStagedCharacter(
    //     req: Omit<DeleteStagedCharacterRequest, "combatId">
    // ) {
    //     return await api.combat.stage.character.delete({
    //         ...req,
    //         combatId: state.combat?.id!,
    //     });
    // }

    // async function startCombat() {
    //     return await api.combat.start({ combatId: state.combat?.id! });
    // }

    // async function finishCombat() {
    //     return await api.combat.finish({ combatId: state.combat?.id! });
    // }

    // async function endTurn() {
    //     return await api.combat.endTurn({ combatId: state.combat?.id! });
    // }

    // async function rollIntoInitiative(
    //     request: Omit<PostRollStagedCharactersIntoInitiativeRequest, "combatId">
    // ) {
    //     return await api.combat.stage.rollIntoInitiative({
    //         ...request,
    //         combatId: state.combat?.id!,
    //     });
    // }

    // async function stagePlannedCharacters(
    //     req: PostStagePlannedCharactersRequest["plannedCharactersToStage"]
    // ) {
    //     return await api.combat.stage.planned({
    //         combatId: state.combat?.id!,
    //         plannedCharactersToStage: req,
    //     });
    // }

    // async function stagePlayerCharacters(playerCharacterIds: string[]) {
    //     return await api.combat.stage.playerCharacters({
    //         combatId: state.combat?.id!,
    //         characterIds: playerCharacterIds,
    //     });
    // }

    // async function updateInitiativeCharacter(character: CombatCharacterDto) {
    //     return await api.combat.initiative.character.update({
    //         combatId: state.combat?.id!,
    //         character,
    //     });
    // }

    // async function deleteInitiativeCharacter(characterId: string) {
    //     return await api.combat.initiative.character.delete({
    //         combatId: state.combat?.id!,
    //         characterId,
    //     });
    // }

    const orderedStagedCharacterListWithPlayerInfo: ComputedRef<
        StagedPlayerDto[]
    > = computed(() => {
        const compareStrings = (a: string, b: string) => {
            let fa = a.toLowerCase(),
                fb = b.toLowerCase();

            if (fa < fb) {
                return -1;
            }
            if (fa > fb) {
                return 1;
            }
            return 0;
        };

        const openCombatCharacterSortFunc = (
            a: StagedPlayerDto,
            b: StagedPlayerDto
        ): number => {
            const aIsDungeonMaster = a.user?.isDungeonMaster;
            const bIsDungeonMaster = b.user?.isDungeonMaster;
            if (aIsDungeonMaster && !bIsDungeonMaster) {
                return -1;
            } else if (!aIsDungeonMaster && bIsDungeonMaster) {
                return 1;
            }

            // First sort by user,
            let result = compareStrings(a.user?.username!, b.user?.username!);
            if (result != 0) {
                return result;
            }

            // Then sort by character name
            result = compareStrings(a.character.name, b.character.name);
            if (result != 0) {
                return result;
            }

            // Sort by copy number
            result =
                (a.character.copyNumber ?? 0) < (b.character.copyNumber ?? 0)
                    ? -1
                    : 1;

            return result;
        };

        return (
            combatQuery.data.value?.combat?.stagedList
                .map(
                    (x) =>
                        ({
                            user: getMemberDetailsFor(
                                x.playerId
                            )!,
                            character: x,
                        }) satisfies StagedPlayerDto
                )
                .sort(openCombatCharacterSortFunc) ?? []
        );
    });

    return {
        campaignId,
        combatId,
        init,
        isLoading,
        campaignQuery,
        combatQuery,
        userIsDm,
        memberDtos,
        orderedStagedCharacterListWithPlayerInfo,
        getMemberDetailsFor,
        combatIsOpen: computed(
            () => combatQuery.data.value?.combat?.state == CombatState.Open
        ),
        combatIsStarted: computed(
            () => combatQuery.data.value?.combat?.state == CombatState.Started
        ),
        combatIsFinished: computed(
            () => combatQuery.data.value?.combat?.state == CombatState.Finished
        ),
        // connection,
        // state,
        // deleteInitiativeCharacter,
        // updateInitiativeCharacter,
        // updateStagedCharacter,
        // addStagedCharacter,
        // deleteStagedCharacter,
        // stagePlannedCharacters,
        // stagePlayerCharacters,
        // setCombat,
        // joinCombat,
        // leaveCombat,
        // startCombat,
        // finishCombat,
        // endTurn,
        // rollIntoInitiative,
        // anyPlannedCharacters: computed(
        //     () =>
        //         (state.combat?.plannedStages.flatMap((x) => x.npcs).length ??
        //             0) > 0
        // ),
        // orderedStagedCharacterListWithPlayerInfo,
        // initiativeListWithPlayerInfo: computed(
        //     () =>
        //         state.combat?.initiativeList.map(
        //             (x) =>
        //                 ({
        //                     user: campaignStore.getMemberDetailsFor(
        //                         x.playerId
        //                     )!,
        //                     character: x,
        //                 }) satisfies InitiativePlayerDto
        //         ) ?? []
        // ),
        isEditableForUser: (charInfo: {
            user: CampaignMemberDto;
            character: InitiativeCharacter | StagedCharacter;
        }) => {
            return (
                userStore.state.user?.userId == combatQuery.data.value?.combat.dungeonMaster ||
                charInfo.user?.userId == userStore.state.user?.userId
            );
        },
        getIconForUser: (charInfo: {
            user: CampaignMemberDto;
            character: InitiativeCharacter | StagedCharacter;
        }) => {
            const currentUserId = userStore.state.user?.userId;

            if (charInfo.user?.userId == combatQuery.data.value?.combat.dungeonMaster) {
                return faCrown;
            }

            if (charInfo.user?.userId == currentUserId) {
                return faCircleUser;
            }

            return faUserLarge;
        },
    };
});
