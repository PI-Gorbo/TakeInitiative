import {
    type QueryClient,
    queryOptions,
    useMutation,
    useQueryClient,
} from "@tanstack/vue-query";
import type { ShallowRef } from "vue";
import type { GetCombatsResponse } from "../api/combat/getCombatsRequest";
import { seconds, type RefOrGetter } from "./utils";
import type { CreatePlannedCombatRequest } from "../api/plannedCombat/createPlannedCombatRequest";
import type { OpenCombatRequest } from "../api/combat/openCombatRequest";
import type {
    PostStagePlayerCharactersRequest,
    PostStagePlayerCharactersResponse,
} from "../api/combat/postStagePlayerCharactersRequest";
import type { GetCombatResponse } from "~/utils/api/combat/getCombatRequest";
import { getCampaignQueryKey } from "~/utils/queries/campaign";
import { AppleIcon } from "lucide-vue-next";

export const getDraftCombatQueryKey = (
    campaignId: MaybeRefOrGetter<string>,
    draftCombatId: MaybeRefOrGetter<string>
) => [campaignId, "combats", "draft", draftCombatId];
export const getDraftCombatQuery = (
    campaignId: RefOrGetter<string>,
    draftComabtId: RefOrGetter<string>
) =>
    queryOptions({
        queryKey: getDraftCombatQueryKey(campaignId, draftComabtId),
        queryFn: () =>
            useApi().draftCombat.get({
                campaignId: toValue(campaignId),
                combatId: toValue(draftComabtId),
            }),
        enabled: () => !!toValue(campaignId) && !!toValue(draftComabtId),
        staleTime: 1000 * 60 * 5, // 5 minutes
    });

export const getAllCombatsQueryKey = (campaignId: MaybeRefOrGetter<string>) => [
    campaignId,
    "combats",
    "all",
];
export const getAllCombatsQuery = (campaignId: RefOrGetter<string>) =>
    queryOptions({
        queryKey: [campaignId, "combats", "all"],
        queryFn: () =>
            useApi().combat.getAll({ campaignId: toValue(campaignId) }),
        enabled: () => !!toValue(campaignId),
        select: (data) => {
            data.combats.sort(sortByFinishedTimestamp);
            return data;
        },
        staleTime: seconds(30),
    });

export const getCombatQueryKey = (
    campaignId: MaybeRefOrGetter<string | null>,
    combatId: MaybeRefOrGetter<string | null>
) => [campaignId, "combats", combatId];
export const getCombatQuery = (
    campaignId: RefOrGetter<string | null>,
    combatId: RefOrGetter<string | null>
) =>
    queryOptions({
        queryKey: getCombatQueryKey(campaignId, combatId),
        queryFn: () => useApi().combat.get({ combatId: toValue(combatId)! }),
        enabled: () => !!toValue(campaignId) && !!toValue(combatId),
        staleTime: seconds(30), // 30 Seconds
    });
export const setCombatQueryData = (
    campaignId: string,
    combatId: string,
    resp: GetCombatResponse,
    queryClient: QueryClient
) => {
    queryClient.setQueryData(getCombatQueryKey(campaignId, combatId), resp);
};

export const createDraftCombatMutation = () => {
    const client = useQueryClient();
    return useMutation({
        mutationFn: (req: CreatePlannedCombatRequest) =>
            useApi().draftCombat.create(req),
        onSuccess: (data, variables) => {
            client.setQueryData(
                getDraftCombatQueryKey(variables.campaignId, data.id),
                data
            );
            client.invalidateQueries({
                queryKey: getAllCombatsQueryKey(variables.campaignId),
            });
        },
    });
};

export const openCombatToPlayersMutation = () => {
    const client = useQueryClient();
    return useMutation({
        mutationFn: (req: OpenCombatRequest) => useApi().combat.open(req),
        onSuccess: (data, variables) => {
            client.invalidateQueries({
                queryKey: getDraftCombatQueryKey(
                    data.combat.campaignId,
                    variables.plannedCombatId
                ),
            });
            client.invalidateQueries({
                queryKey: getAllCombatsQueryKey(data.combat.campaignId),
            });
            client.setQueryData(
                getCombatQueryKey(data.combat.campaignId, data.combat.id),
                data.combat
            );
        },
    });
};

function sortByFinishedTimestamp(
    a: GetCombatsResponse["combats"][number],
    b: GetCombatsResponse["combats"][number]
) {
    if (a.finishedTimestamp == null && b.finishedTimestamp != null) {
        return -1;
    }

    if (a.finishedTimestamp != null && b.finishedTimestamp == null) {
        return 1;
    }

    if (a.finishedTimestamp == b.finishedTimestamp) {
        return 0;
    }

    if (a.finishedTimestamp! < b.finishedTimestamp!) {
        return -1;
    }

    if (a.finishedTimestamp! > b.finishedTimestamp!) {
        return 1;
    }

    return 0;
}

// Mutations for when a comabt is started / starting.
export const useStagePlayerCharacterMutation = () => {
    const client = useQueryClient();
    const api = useApi();
    return useMutation({
        mutationFn: (req: PostStagePlayerCharactersRequest) =>
            api.combat.stage.playerCharacters(req),
        onSuccess: (data, request) => {
            setCombatQueryData(
                data.combat.campaignId,
                data.combat.id,
                data,
                client
            );
        },
    });
};

export const useAddStagedCharactersToCombatMutation = () => {
    const client = useQueryClient();
    const api = useApi();
    return useMutation({
        mutationFn: api.combat.stage.rollIntoInitiative,
        onSuccess: (data, request) => {
            setCombatQueryData(
                data.combat.campaignId,
                data.combat.id,
                data,
                client
            );
        },
    });
};

export const useEndTurnMutation = () => {
    const client = useQueryClient();
    const api = useApi();
    return useMutation({
        mutationFn: api.combat.endTurn,
        onSuccess: (data, request) => {
            setCombatQueryData(
                data.combat.campaignId,
                data.combat.id,
                data,
                client
            );
        },
    });
};

export const useStartCombatMutation = () => {
    const client = useQueryClient();
    const api = useApi();
    return useMutation({
        mutationFn: api.combat.start,
        onSuccess: (data, request) => {
            setCombatQueryData(
                data.combat.campaignId,
                data.combat.id,
                data,
                client
            );
        },
    });
};

export const useFinishCombatMutation = () => {
    const client = useQueryClient();
    const api = useApi();
    return useMutation({
        mutationFn: api.combat.finish,
        onSuccess: (data, request) => {
            setCombatQueryData(
                data.combat.campaignId,
                data.combat.id,
                data,
                client
            );
            client.invalidateQueries({
                queryKey: getCampaignQueryKey(data.combat.campaignId),
            });
        },
    });
};

export const useAddPlannedCharacterToCombatMutation = () => {
    const client = useQueryClient();
    const api = useApi();
    return useMutation({
        mutationFn: api.combat.stage.planned,
        onSuccess: (data, request) => {
            setCombatQueryData(
                data.combat.campaignId,
                data.combat.id,
                data,
                client
            );
        },
    });
};

export const useDeleteStagedCharacterMutation = () => {
    const client = useQueryClient();
    const api = useApi();
    return useMutation({
        mutationFn: api.combat.stage.character.delete,
        onSuccess: (data, request) => {
            setCombatQueryData(
                data.combat.campaignId,
                data.combat.id,
                data,
                client
            );
        },
    });
};

export const useEditStagedCharacterMutation = () => {
    const client = useQueryClient();
    const api = useApi();
    return useMutation({
        mutationFn: api.combat.stage.character.update,
        onSuccess: (data, request) => {
            setCombatQueryData(
                data.combat.campaignId,
                data.combat.id,
                data,
                client
            );
        },
    });
};

export const useAddStagedCharacterMutation = () => {
    const client = useQueryClient();
    const api = useApi();
    return useMutation({
        mutationFn: api.combat.stage.character.add,
        onSuccess: (data, request) => {
            setCombatQueryData(
                data.combat.campaignId,
                data.combat.id,
                data,
                client
            );
        },
    });
};

export const useUpdateInitiativeCharacterMutation = () => {
    const client = useQueryClient();
    const api = useApi();
    return useMutation({
        mutationFn: api.combat.initiative.character.update,
        onSuccess: (data, request) => {
            setCombatQueryData(
                data.combat.campaignId,
                data.combat.id,
                data,
                client
            );
        },
    });
};

export const useDeleteInitiativeCharacterMutation = () => {
    const client = useQueryClient();
    const api = useApi();
    return useMutation({
        mutationFn: api.combat.initiative.character.delete,
        onSuccess: (data, request) => {
            setCombatQueryData(
                data.combat.campaignId,
                data.combat.id,
                data,
                client
            );
        },
    });
};
