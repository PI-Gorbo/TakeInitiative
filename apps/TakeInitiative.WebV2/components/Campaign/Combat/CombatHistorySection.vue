<template>
    <section class="flex flex-col gap-2">
        <header>
            <FontAwesomeIcon :icon="faFlagCheckered" />
            <span> Combat History </span>
        </header>
        <template v-if="(props.combatList ?? []).length !== 0">
            <ul class="flex flex-col gap-2">
                <Button
                    variant="outline"
                    class="h-fit flex justify-between w-full items-center disabled:border-primary disabled:opacity-100"
                    v-for="historicalCombat in props.combatList ?? []"
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
                v-if="(props.combatList ?? []).length === 0"
                class="text-gray-500">
                None yet...
            </span>
        </div>
    </section>
</template>
<script setup lang="ts">
    const router = useRouter();
    import { faFlagCheckered } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import type { CombatHistoryDto } from "~/utils/api/combat/getCombatsRequest";

    const props = defineProps<{
        campaignId: string;
        combatList: CombatHistoryDto;
        combatDisabledFunc?: (historicalCombatId: string) => boolean;
        interactableFunc?: (historicalCombatId: string) => boolean;
    }>();
</script>
