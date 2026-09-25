import * as signalR from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/vue-query";
import { getCampaignQueryKey, getCampaignsQueryKey } from "~/utils/queries/campaign";

/**
 * Keeps the open campaign live over `CampaignHub`: joins the `campaign:{id}` group
 * for the given campaign and refreshes the campaign query when a member joins or
 * the owner changes a role. Call it once, from the campaign layout.
 */
export function useCampaignHub(campaignId: MaybeRefOrGetter<string | undefined>) {
    const queryClient = useQueryClient();
    const joinedCampaignId = ref<string | null>(null);

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(`${useRuntimeConfig().public.axios.baseURL}/campaignHub`, {
            accessTokenFactory: () => useCookie(".AspNetCore.Cookies").value!,
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    const refreshCampaign = () =>
        queryClient.invalidateQueries({
            queryKey: getCampaignQueryKey(joinedCampaignId.value),
        });

    // Message names are `CampaignHubMessages` in the API. Handlers must return
    // nothing: SignalR logs an error when a handler returns a value (a promise too).
    connection.on("memberJoined", () => {
        void refreshCampaign();
    });
    connection.on("memberRoleChanged", () => {
        void refreshCampaign();
        // The caller's own role may have changed; the campaign list shows it.
        void queryClient.invalidateQueries({ queryKey: getCampaignsQueryKey() });
    });
    connection.onreconnected(async () => {
        // A reconnect leaves every group, so join again and catch up.
        if (joinedCampaignId.value) {
            await connection.invoke("Join", joinedCampaignId.value);
        }
        await refreshCampaign();
    });

    async function join(id: string) {
        if (connection.state === signalR.HubConnectionState.Disconnected) {
            await connection.start();
        }
        await connection.invoke("Join", id);
        joinedCampaignId.value = id;
    }

    async function leave() {
        const id = joinedCampaignId.value;
        joinedCampaignId.value = null;
        if (id && connection.state === signalR.HubConnectionState.Connected) {
            await connection.invoke("Leave", id);
        }
    }

    watch(
        () => toValue(campaignId),
        async (id) => {
            if (!id || id === joinedCampaignId.value) return;
            try {
                await leave();
                await join(id);
            } catch (err) {
                console.warn("Could not join the campaign hub.", err);
            }
        },
        { immediate: true }
    );

    onScopeDispose(async () => {
        await leave().catch(() => {});
        await connection.stop();
    });

    return { connection };
}
