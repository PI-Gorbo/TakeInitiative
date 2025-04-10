import { queryOptions } from "@tanstack/vue-query";
import type { ShallowRef } from "vue";
import type { GetCombatsResponse } from "../api/combat/getCombatsRequest";
import type { RefOrGetter } from "./utils";

export const combatQueries = {
    getAllCombatsQuery: (campaignId: RefOrGetter<string>) => queryOptions({
        queryKey: [campaignId, 'combats', 'all'],
        queryFn: () => useApi().combat.getAll({ campaignId: toValue(campaignId) }),
        enabled: () => !!toValue(campaignId),
        select: (data) => {
            data.combats.sort(sortByFinishedTimestamp);
            return data;
        },
        staleTime: 1000 * 60 * 5, // 5 minutes
    }),
    getDraftCombat: {
        key: (campaignId: string, plannedCombatId: string) => [campaignId, 'combats', 'planned', plannedCombatId],
        query: (campaignId: RefOrGetter<string>, plannedCombatId: RefOrGetter<string>) => queryOptions({
            queryKey: [campaignId, 'combats', 'planned', plannedCombatId],
            queryFn: () => useApi().draftCombat.get({ campaignId: toValue(campaignId), combatId: toValue(plannedCombatId) }),
            enabled: () => !!toValue(campaignId) && !!toValue(plannedCombatId),
            staleTime: 1000 * 60 * 5, // 5 minutes
        })
    }
     const addStage = useMutation({
         mutationFn: async (
             req: Omit<CreatePlannedCombatStageRequest, "combatId">
         ) =>
             await api.draftCombat.stage.create({
                 ...req,
                 combatId: route.params.draftCombatId,
             }),

         onSuccess(resp) {
             queryClient.setQueryData(
                 combatQueries.getDraftCombat.key(
                     route.params.id,
                     route.params.draftCombatId
                 ),
                 resp
             );
         },
     });
    function onAddStage() {
        const stages = draftCombatQuery.data.value!.stages;
const filteredStages = stages
    .map((stage) => stage.name)
    .filter((name) => name.startsWith("Stage "));

if (filteredStages.length === 0) {
    return addStage.mutateAsync({
        name: "Stage 1",
    });
}

const availableNumbers = filteredStages
    .map((name) => parseInt(name.split(" ")[1]))
    .filter((number) => !Number.isNaN(number));

if (availableNumbers.length === 0) {
    return addStage.mutateAsync({
        name: "Stage 1",
    });
}

const biggestNumber = availableNumbers.sort((a, b) => -(a - b))[0];

return addStage.mutateAsync({
    name: `Stage ${biggestNumber + 1}`,
});
    }

const addNpc = useMutation({
    mutationFn: async (req: {
        stage: DraftCombatStage;
        nonPlayerCharacter: Omit<
            CreatePlannedCombatNpcRequest,
            "combatId" | "stageId"
        >;
    }) =>
        await api.draftCombat.stage.npc.create({
            combatId: route.params.draftCombatId,
            stageId: req.stage.id,
            ...req.nonPlayerCharacter,
        }),
    onSuccess(resp) {
        queryClient.setQueryData(
            combatQueries.getDraftCombat.key(
                route.params.id,
                route.params.draftCombatId
            ),
            resp
        );
    },
});

const updateNpc = useMutation({
    mutationFn: async (req: {
        stage: DraftCombatStage;
        npc: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">;
    }) =>
        await api.draftCombat.stage.npc.update({
            combatId: route.params.draftCombatId,
            stageId: req.stage.id,
            ...req.npc,
        }),
    onSuccess(resp) {
        queryClient.setQueryData(
            combatQueries.getDraftCombat.key(
                route.params.id,
                route.params.draftCombatId
            ),
            resp
        );
    },
});

const deleteNpc = useMutation({
    mutationFn: async (req: {
        stage: DraftCombatStage;
        npc: Omit<DeletePlannedCombatNpcRequest, "combatId" | "stageId">;
    }) =>
        await api.draftCombat.stage.npc.delete({
            combatId: route.params.draftCombatId,
            stageId: req.stage.id,
            ...req.npc,
        }),
    onSuccess(resp) {
        queryClient.setQueryData(
            combatQueries.getDraftCombat.key(
                route.params.id,
                route.params.draftCombatId
            ),
            resp
        );
    },
});

const deleteStage = useMutation({
    mutationFn: async (stage: DraftCombatStage) =>
        await api.draftCombat.stage.delete({
            combatId: route.params.draftCombatId,
            stageId: stage.id,
        }),
    onSuccess(resp) {
        queryClient.setQueryData(
            combatQueries.getDraftCombat.key(
                route.params.id,
                route.params.draftCombatId
            ),
            resp
        );
    },
});

const updateStage = useMutation({
    mutationFn: async (req: { stageId: string; name: string }) => {
        return await api.draftCombat.stage.update({
            combatId: route.params.draftCombatId,
            stageId: req.stageId,
            name: req.name,
        });
    },
    onSuccess(resp) {
        queryClient.setQueryData(
            combatQueries.getDraftCombat.key(
                route.params.id,
                route.params.draftCombatId
            ),
            resp
        );
    },
});

const updateDraftCombatName = useMutation({
    mutationFn: async (name: string) => {
        return await api.draftCombat.update({
            plannedCombatId: route.params.draftCombatId,
            combatName: name,
        });
    },
    onSuccess(resp) {
        queryClient.setQueryData(
            combatQueries.getDraftCombat.key(
                route.params.id,
                route.params.draftCombatId
            ),
            resp
        );
        queryClient.invalidateQueries({
            queryKey: [route.params.id, "combats", "all"],
        });
    },
});
}

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

