import { StagedCharacterDTO } from "./../utils/api/combat/putUpsertStagedCharacter";
import type { Combat } from "~/utils/types/models";
import * as signalR from "@microsoft/signalr";
import type { UpsertStagedCharacterRequest } from "~/utils/api/combat/putUpsertStagedCharacter";
import type { DeleteStagedCharacterRequest } from "~/utils/api/combat/deleteStagedCharacterRequest";

export const useCombatStore = defineStore("combatStore", () => {
    const userStore = useUserStore();
    const campaignStore = useCampaignStore();
    const api = useApi();

    // Start the connection.
    var connection = new signalR.HubConnectionBuilder()
        .withUrl(`${useRuntimeConfig().public.axios.baseURL}/combatHub`, {
            accessTokenFactory: () => useCookie(".AspNetCore.Cookies").value!,
        })
        .build();

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
            userStore.setSelectedCampaign(resp.combat.campaignId);
            await campaignStore.setCampaignById(resp.combat.campaignId);
        });
    }

    async function joinCombat(): Promise<void> {
        if (connection.state != signalR.HubConnectionState.Connected) {
            await startConnection();
        }

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
        connection.on("combatUpdated", (combat: Combat) => {
            state.combat = combat;
            return;
        });
    }

    async function upsertStagedCharacter(req: StagedCharacterDTO) {
        return await api.combat.stagedCharacters.upsert({
            character: req,
            combatId: state.combat?.id!,
        });
    }

    async function deleteStagedCharacter(
        req: Omit<DeleteStagedCharacterRequest, "combatId">,
    ) {
        return await api.combat.stagedCharacters.delete({
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

    async function nextTurn() {
        return await api.combat.nextTurn({ combatId: state.combat?.id! });
    }

    return {
        connection,
        state,
        upsertStagedCharacter,
        deleteStagedCharacter,
        setCombat,
        joinCombat,
        leaveCombat,
        startCombat,
        finishCombat,
        nextTurn,
    };
});
