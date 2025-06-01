<template>
    <section class="flex flex-col gap-2">
        <header>
            <FontAwesomeIcon :icon="faFlagCheckered" />
            <span> Combat History </span>
        </header>
        <LoadingFallback :isLoading="allCombatsQuery.isLoading.value" iconSize="2x">
            <template v-if="(allCombatsQuery.data.value?.combats ?? []).length !== 0">
                <ul class="flex flex-col gap-2">
                    <Button
                        variant="outline"
                        class="h-fit flex justify-between w-full items-center disabled:border-primary disabled:opacity-100"
                        v-for="historicalCombat in allCombatsQuery.data.value?.combats ?? []"
                        :key="historicalCombat.combatId"
                        :disabled="
                            combatDisabledFunc
                                ? combatDisabledFunc(historicalCombat.combatId)
                                : false
                        "
                        :class="[
                            {
                                interactable: interactableFunc
                                    ? interactableFunc(historicalCombat.combatId)
                                    : true,
                            },
                        ]"
                        @click="
                            () =>
                                router.push({
                                    name: 'app-campaigns-campaignId-combats-history-combatId',
                                    params: {
                                        campaignId: props.campaignId,
                                        combatId: historicalCombat.combatId,
                                    },
                                })
                        ">
                        {{ historicalCombat.combatName }}
                    </Button>
                </ul>
            </template>
            <div :class="['flex flex-1 gap-1 items-center']">
                <span
                    v-if="(allCombatsQuery.data.value?.combats ?? []).length === 0"
                    class="text-gray-500">
                    None yet...
                </span>
            </div>
        </LoadingFallback>
    </section>
</template>
<script setup lang="ts">
    const router = useRouter();
    import { faFlagCheckered } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useQuery } from "@tanstack/vue-query";
    import type { CombatHistoryDto } from "~/utils/api/combat/getCombatsRequest";
    import { getAllCombatsQuery } from "~/utils/queries/combats";
    const props = defineProps<{
        campaignId: string;
        combatDisabledFunc?: (historicalCombatId: string) => boolean;
        interactableFunc?: (historicalCombatId: string) => boolean;
    }>();

    const allCombatsQuery = useQuery(
        getAllCombatsQuery(() => props.campaignId)
    );
</script>
