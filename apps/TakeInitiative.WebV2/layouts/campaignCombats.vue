<template>
    <div class="w-full h-full">
        <NuxtLayout name="campaign">
            <LoadingFallback
                container="main"
                :isLoading="combatsQuery.isLoading.value">
                <section
                    class="lg:grid w-full flex flex-col gap-4 lg:grid-cols-3 pb-2">
                    <div
                        v-if="
                            isLargeScreen ||
                            route.name === 'app-campaigns-id-combats'
                        "
                        class="lg:col-span-1 lg:col-start-1 lg:row-start-1">
                        <Card class="p-4 border-primary/50 flex flex-col gap-4">
                            <section>
                                <header>
                                    <FontAwesomeIcon :icon="faPenToSquare" />
                                    <span> Draft Combats </span>
                                </header>

                                <template
                                    v-if="
                                        (
                                            combatsQuery.data.value
                                                ?.plannedCombats ?? []
                                        ).length > 0
                                    ">
                                    <ul class="flex flex-col gap-2">
                                        <Button
                                            v-for="plannedCombat in combatsQuery
                                                .data.value!.plannedCombats"
                                            :key="plannedCombat.id"
                                            variant="outline"
                                            class="h-fit flex justify-between w-full items-center disabled:border-gold disabled:opacity-100"
                                            :disabled="
                                                currentDraftCombat ===
                                                plannedCombat.id
                                            "
                                            :class="[
                                                {
                                                    interactable:
                                                        currentDraftCombat ===
                                                            undefined ||
                                                        currentDraftCombat !==
                                                            plannedCombat.id,
                                                },
                                            ]"
                                            @click="
                                                () =>
                                                    router.push({
                                                        name: 'app-campaigns-id-combats-drafts-draftCombatId',
                                                        params: {
                                                            id: route.params.id,
                                                            draftCombatId:
                                                                plannedCombat.id,
                                                        },
                                                    })
                                            ">
                                            {{ plannedCombat.name }}
                                        </Button>
                                    </ul>
                                </template>
                                <div
                                    :class="[
                                        'flex flex-1 gap-1 items-center',
                                        (
                                            combatsQuery.data.value
                                                ?.plannedCombats ?? []
                                        ).length === 0
                                            ? 'justify-between'
                                            : 'justify-end',
                                    ]">
                                    <span
                                        v-if="
                                            (
                                                combatsQuery.data.value
                                                    ?.plannedCombats ?? []
                                            ).length === 0
                                        "
                                        class="text-gray-500">
                                        None yet...
                                    </span>
                                    <!-- <Button
                                        variant="link"
                                        @click="showCreatePlannedCombatModal">
                                        <FontAwesomeIcon :icon="faPlusCircle" />
                                        New
                                    </Button> -->
                                </div>
                            </section>

                            <section>
                                <header>
                                    <FontAwesomeIcon :icon="faFlagCheckered" />
                                    <span> Combat History </span>
                                </header>

                                <template
                                    v-if="
                                        (combatsQuery.data.value?.combats ?? [])
                                            .length !== 0
                                    ">
                                    <ul>
                                        <Button
                                            variant="outline"
                                            class="h-fit flex justify-between w-full items-center interactable"
                                            v-for="historicalCombat in combatsQuery
                                                .data.value?.combats ?? []"
                                            :key="historicalCombat.combatId">
                                            {{ historicalCombat.combatName }}
                                        </Button>
                                    </ul>
                                </template>
                                <div
                                    :class="['flex flex-1 gap-1 items-center']">
                                    <span
                                        v-if="
                                            (
                                                combatsQuery.data.value
                                                    ?.combats ?? []
                                            ).length === 0
                                        "
                                        class="text-gray-500">
                                        None yet...
                                    </span>
                                </div>
                            </section>
                        </Card>
                    </div>
                    <div
                        v-show="
                            route.name !== 'app-campaigns-id-combats' ||
                            !hasAnyCombats
                        "
                        class="lg:block lg:col-span-2 lg:col-start-2">
                        <NuxtPage />
                    </div>
                </section>
            </LoadingFallback>
        </NuxtLayout>
        <!-- <Dialog v-model:open="modalState.modalOpen">
            <DialogContent class="flex flex-col gap-2">
                <DialogHeader> Create a planned combat. </DialogHeader>

                <CampaignCombatCreatePlannedCombatForm
                    :onCreatePlannedCombat="onCreatePlannedCombat" />
            </DialogContent>
        </Dialog> -->
    </div>
</template>
<script setup lang="ts">
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useMediaQuery } from "@vueuse/core";
    import {
        faFlagCheckered,
        faPenToSquare,
    } from "@fortawesome/free-solid-svg-icons";
    import { combatQueries } from "~/utils/queries/combats";
    import { useQuery } from "@tanstack/vue-query";

    // Stores
    const router = useRouter();
    const isLargeScreen = useMediaQuery("(min-width: 1024px)");
    const route = useRoute("app-campaigns-id-combats");
    const draftCombatRoute = useRoute(
        "app-campaigns-id-combats-drafts-draftCombatId"
    );
    const currentDraftCombat: ComputedRef<string | null> = computed(
        () => draftCombatRoute.params.draftCombatId
    );

    const combatsQuery = useQuery(
        combatQueries.getAllCombatsQuery(() => {
            console.log("here");
            return route.params.id;
        })
    );

    const hasAnyCombats = computed(
        () =>
            (combatsQuery.data.value?.combats.length ?? 0) > 0 ||
            (combatsQuery.data.value?.plannedCombats.length ?? 0) > 0
    );
</script>
