import { deleteInitiativeCharacterRequest } from "../utils/api/combat/deleteInitiativeCharacterRequest";
import {
    CombatState,
    type Combat,
    type InitiativeCharacter,
    type StagedCharacter,
} from "../utils/types/models";
import * as signalR from "@microsoft/signalr";
import type {
    StagedCharacterDTO,
    UpdateStagedCharacterRequest,
} from "../utils/api/combat/putUpsertStagedCharacter";
import type { DeleteStagedCharacterRequest } from "../utils/api/combat/deleteStagedCharacterRequest";
import type { PostStagePlannedCharactersRequest } from "../utils/api/combat/postStagePlannedCharactersRequest";
import type { CampaignMemberDto } from "../utils/api/campaign/getCampaignRequest";
import type { PostRollStagedCharactersIntoInitiativeRequest } from "../utils/api/combat/postRollStagedCharactersIntoInitiative";
import type {
    CombatCharacterDto,
    PutUpdateInitiativeCharacterRequest,
} from "../utils/api/combat/putUpdateInitiativeCharacterRequest";
import type { StagedCharacterWithoutIdDTO } from "~/utils/api/combat/postAddStagedCharacter";
export type InitiativePlayerDto = {
    user: CampaignMemberDto;
    character: InitiativeCharacter;
};
export type StagedPlayerDto = {
    user: CampaignMemberDto;
    character: StagedCharacter;
};

export const useCombatStore = defineStore("combatStore", () => {
    const userStore = useUserStore();
    const campaignStore = useCampaignStore();
    const api = useApi();

    // Start the connection.
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(`${useRuntimeConfig().public.axios.baseURL}/combatHub`, {
            accessTokenFactory: () => useCookie(".AspNetCore.Cookies").value!,
        })
        .withAutomaticReconnect()
        .build();

    connection.onreconnected(async () => {
        await setCombat(state.combat?.id!);
        await connection.send(
            // Rejoin, as users are kicked from all groups on disconnect
            "joinCombat",
            userStore.state.user?.userId,
            state.combat?.id
        );
    });

    connection.on("combatUpdated", (combat: Combat) => {
        state.combat = combat;
        return;
    });

    const state = reactive<{
        combat: Combat | null;
        signalRError: string | null;
    }>({
        combat: null,
        signalRError: null,
    });

    async function setCombat(combatId: string): Promise<void> {
        return await api.combat.get({ combatId }).then(async (resp) => {
            state.combat = resp.combat;
            await campaignStore.setCampaignById(resp.combat.campaignId);
        });
    }

    async function joinCombat(): Promise<void> {
        await startConnection();

        const userId = userStore.state.user?.userId;

        return await connection
            .send("joinCombat", userId, state.combat?.id)
            .catch((error) => (state.signalRError = error));
    }

    async function leaveCombat(): Promise<void> {
        if (connection.state != signalR.HubConnectionState.Connected) return;

        const userId = userStore.state.user?.userId;
        if (state.combat?.currentPlayers.find((x) => x.userId) == null) {
            return Promise.resolve();
        }

        return await connection
            .send("leaveCombat", userId, state.combat?.id)
            .then(() => connection.stop());
    }

    async function startConnection() {
        if (
            connection.state == signalR.HubConnectionState.Connecting ||
            connection.state == signalR.HubConnectionState.Connected
        ) {
            return;
        }

        await connection.start().catch((error) => {
            state.signalRError = error;
            throw error;
        });
    }

    async function updateStagedCharacter(req: StagedCharacterDTO) {
        return await api.combat.stage.character.update({
            character: req,
            combatId: state.combat?.id!,
        });
    }

    async function addStagedCharacter(req: StagedCharacterWithoutIdDTO) {
        return await api.combat.stage.character.add({
            character: req,
            combatId: state.combat?.id!,
        });
    }

    async function deleteStagedCharacter(
        req: Omit<DeleteStagedCharacterRequest, "combatId">
    ) {
        return await api.combat.stage.character.delete({
            ...req,
            combatId: state.combat?.id!,
        });
    }

    async function startCombat() {
        return await api.combat.start({ combatId: state.combat?.id! });
    }

    async function finishCombat() {
        return await api.combat.finish({ combatId: state.combat?.id! });
    }

    async function endTurn() {
        return await api.combat.endTurn({ combatId: state.combat?.id! });
    }

    async function rollIntoInitiative(
        request: Omit<PostRollStagedCharactersIntoInitiativeRequest, "combatId">
    ) {
        return await api.combat.stage.rollIntoInitiative({
            ...request,
            combatId: state.combat?.id!,
        });
    }

    async function stagePlannedCharacters(
        req: PostStagePlannedCharactersRequest["plannedCharactersToStage"]
    ) {
        return await api.combat.stage.planned({
            combatId: state.combat?.id!,
            plannedCharactersToStage: req,
        });
    }

    async function stagePlayerCharacters(playerCharacterIds: string[]) {
        return await api.combat.stage.playerCharacters({
            combatId: state.combat?.id!,
            characterIds: playerCharacterIds,
        });
    }

    async function updateInitiativeCharacter(character: CombatCharacterDto) {
        return await api.combat.initiative.character.update({
            combatId: state.combat?.id!,
            character,
        });
    }

    async function deleteInitiativeCharacter(characterId: string) {
        return await api.combat.initiative.character.delete({
            combatId: state.combat?.id!,
            characterId,
        });
    }

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
            state.combat?.stagedList
                .map(
                    (x) =>
                        ({
                            user: campaignStore.getMemberDetailsFor(
                                x.playerId
                            )!,
                            character: x,
                        }) satisfies StagedPlayerDto
                )
                .sort(openCombatCharacterSortFunc) ?? []
        );
    });

    return {
        connection,
        state,
        deleteInitiativeCharacter,
        updateInitiativeCharacter,
        updateStagedCharacter,
        addStagedCharacter,
        deleteStagedCharacter,
        stagePlannedCharacters,
        stagePlayerCharacters,
        setCombat,
        joinCombat,
        leaveCombat,
        startCombat,
        finishCombat,
        endTurn,
        rollIntoInitiative,
        combatIsOpen: computed(() => state.combat?.state == CombatState.Open),
        combatIsStarted: computed(
            () => state.combat?.state == CombatState.Started
        ),
        combatIsFinished: computed(
            () => state.combat?.state == CombatState.Finished
        ),
        userIsDm: computed(
            () => userStore.state.user?.userId == state.combat!.dungeonMaster
        ),
        combat: computed(() => {
            return state.combat;
        }),
        anyPlannedCharacters: computed(
            () =>
                (state.combat?.plannedStages.flatMap((x) => x.npcs).length ??
                    0) > 0
        ),
        orderedStagedCharacterListWithPlayerInfo,
        initiativeListWithPlayerInfo: computed(
            () =>
                state.combat?.initiativeList.map(
                    (x) =>
                        ({
                            user: campaignStore.getMemberDetailsFor(
                                x.playerId
                            )!,
                            character: x,
                        }) satisfies InitiativePlayerDto
                ) ?? []
        ),
        isEditableForUser: (charInfo: {
            user: CampaignMemberDto;
            character: InitiativeCharacter | StagedCharacter;
        }) => {
            return (
                userStore.state.user?.userId == state.combat?.dungeonMaster ||
                charInfo.user?.userId == userStore.state.user?.userId
            );
        },
        getIconForUser: (charInfo: {
            user: CampaignMemberDto;
            character: InitiativeCharacter | StagedCharacter;
        }) => {
            const currentUserId = userStore.state.user?.userId;

            if (charInfo.user?.userId == state.combat?.dungeonMaster) {
                return "crown";
            }

            if (charInfo.user?.userId == currentUserId) {
                return "circle-user";
            }

            return "user-large";
        },
    };
});
