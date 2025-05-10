<template>
    <div class="w-full h-full">
        <NuxtLayout name="campaign">
            <LoadingFallback
                container="main"
                :isLoading="
                    combatsQuery.isLoading.value ||
                    campaignQuery.isLoading.value
                ">
                <section
                    class="lg:grid w-full flex flex-col gap-4 lg:grid-cols-3 pb-2">
                    <div
                        v-if="
                            isLargeScreen ||
                            route.name === 'app-campaigns-campaignId-combats'
                        "
                        class="lg:col-span-1 lg:col-start-1 lg:row-start-1">
                        <div class="flex flex-col gap-6">
                            <CampaignCombatJoinBanner
                                :campaignId="route.params.campaignId"
                                :combatInfo="
                                    campaignQuery.data.value
                                        ?.currentCombatInfo ?? null
                                " />
                            <Card
                                class="p-4 border-primary/50 flex flex-col gap-4">
                                <section class="flex flex-col gap-2">
                                    <header>
                                        <FontAwesomeIcon
                                            :icon="faPenToSquare" />
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
                                            <li
                                                v-for="plannedCombat in combatsQuery
                                                    .data.value!.plannedCombats"
                                                :key="plannedCombat.id"
                                                class="flex gap-1">
                                                <Button
                                                    class="h-fit flex justify-between w-full items-center disabled:border-primary disabled:opacity-100"
                                                    variant="outline"
                                                    :disabled="
                                                        currentDraftCombat ===
                                                        plannedCombat.id
                                                    "
                                                    :class="[
                                                        {
                                                            interactable:
                                                                currentDraftCombat !==
                                                                    plannedCombat.id &&
                                                                (isLargeScreen ||
                                                                    currentDraftCombat ===
                                                                        undefined ||
                                                                    currentDraftCombat !==
                                                                        plannedCombat.id),
                                                        },
                                                    ]"
                                                    @click="
                                                        () =>
                                                            router.push({
                                                                name: 'app-campaigns-campaignId-combats-drafts-draftCombatId',
                                                                params: {
                                                                    campaignId:
                                                                        route
                                                                            .params
                                                                            .campaignId,
                                                                    draftCombatId:
                                                                        plannedCombat.id,
                                                                },
                                                            })
                                                    ">
                                                    {{ plannedCombat.name }}
                                                </Button>
                                                <ConfirmationButton
                                                    v-if="
                                                        campaignQuery.isSuccess
                                                            .value &&
                                                        campaignQuery.data.value
                                                            ?.currentCombatInfo ==
                                                            undefined
                                                    "
                                                    :triggerButtonProps="{
                                                        variant: 'outline',
                                                        class: 'interactable',
                                                        disabled:
                                                            !!hasALiveCombat,
                                                    }"
                                                    :cancelButtonProps="{
                                                        variant: 'destructive',
                                                    }"
                                                    :confirmButtonProps="{
                                                        onclick: () => {
                                                            openDraftCombat({
                                                                plannedCombatId:
                                                                    plannedCombat.id,
                                                            });
                                                        },
                                                    }">
                                                    <template #Title
                                                        >Open combat to players
                                                    </template>
                                                    <template #Description>
                                                        By opening the combat to
                                                        your players, they can
                                                        join the combat page and
                                                        add their characters to
                                                        the combat before it
                                                        begins.
                                                    </template>
                                                    <template #TriggerButton>
                                                        <FontAwesomeIcon
                                                            :icon="faRocket" />
                                                    </template>
                                                    <template #CancelButton>
                                                        Cancel
                                                    </template>
                                                    <template #ConfirmButton>
                                                        {{
                                                            draftCombatHelper
                                                                .openDraftMutation
                                                                .isPending.value
                                                                ? "Opening..."
                                                                : "Open combat to players"
                                                        }}
                                                    </template>
                                                </ConfirmationButton>
                                            </li>
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
                                        <Button
                                            variant="link"
                                            @click="
                                                () =>
                                                    (modalState.modalOpen = true)
                                            ">
                                            <FontAwesomeIcon
                                                :icon="faPlusCircle" />
                                            New
                                        </Button>
                                    </div>
                                </section>
                                <section>
                                    <header>
                                        <FontAwesomeIcon
                                            :icon="faFlagCheckered" />
                                        <span> Combat History </span>
                                    </header>
                                    <template
                                        v-if="
                                            (
                                                combatsQuery.data.value
                                                    ?.combats ?? []
                                            ).length !== 0
                                        ">
                                        <ul>
                                            <Button
                                                variant="outline"
                                                class="h-fit flex justify-between w-full items-center interactable"
                                                v-for="historicalCombat in combatsQuery
                                                    .data.value?.combats ?? []"
                                                :key="
                                                    historicalCombat.combatId
                                                ">
                                                {{
                                                    historicalCombat.combatName
                                                }}
                                            </Button>
                                        </ul>
                                    </template>
                                    <div
                                        :class="[
                                            'flex flex-1 gap-1 items-center',
                                        ]">
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
                    </div>
                    <div
                        v-show="
                            route.name !== 'app-campaigns-campaignId-combats' ||
                            !hasAnyCombats
                        "
                        class="lg:block lg:col-span-2 lg:col-start-2">
                        <NuxtPage />
                    </div>
                </section>
            </LoadingFallback>
        </NuxtLayout>
        <Dialog v-model:open="modalState.modalOpen">
            <DialogContent class="flex flex-col gap-2">
                <DialogHeader> Create a planned combat.</DialogHeader>

                <CampaignCombatDraftCreateForm
                    :campaignId="route.params.campaignId"
                    :onCreateDraftCombat="createPlannedCombat" />
            </DialogContent>
        </Dialog>
    </div>
</template>
<script setup lang="ts">
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { reactiveComputed, useMediaQuery } from "@vueuse/core";
    import {
        faFistRaised,
        faFlagCheckered,
        faPenToSquare,
        faPlusCircle,
        faRocket,
    } from "@fortawesome/free-solid-svg-icons";
    import {
        createDraftCombatMutation,
        getAllCombatsQuery,
    } from "~/utils/queries/combats";
    import { useQuery } from "@tanstack/vue-query";
    import type { CreatePlannedCombatRequest } from "~/utils/api/plannedCombat/createPlannedCombatRequest";
    import { CombatState } from "~/utils/types/models";
    import type { OpenCombatRequest } from "~/utils/api/combat/openCombatRequest";
    import { toast } from "vue-sonner";
    import { getCampaignQuery } from "~/utils/queries/campaign";

    // Stores
    const router = useRouter();
    const isLargeScreen = useMediaQuery("(min-width: 1024px)");
    const route = useRoute("app-campaigns-campaignId-combats");
    const draftCombatRoute = useRoute(
        "app-campaigns-campaignId-combats-drafts-draftCombatId"
    );
    const currentDraftCombat: ComputedRef<string | null> = computed(
        () => draftCombatRoute.params.draftCombatId
    );

    const campaignQuery = useQuery(
        getCampaignQuery(() => route.params.campaignId)
    );
    const combatsQuery = useQuery(
        getAllCombatsQuery(() => {
            return route.params.campaignId;
        })
    );

    const hasALiveCombat = computed(() =>
        combatsQuery.data.value?.combats.some(
            (x) => x.state !== CombatState.Finished
        )
    );

    const hasAnyCombats = computed(
        () =>
            (combatsQuery.data.value?.combats.length ?? 0) > 0 ||
            (combatsQuery.data.value?.plannedCombats.length ?? 0) > 0
    );

    const modalState = reactive({
        modalOpen: false,
    });

    const draftCombatHelper = useDraftCombatHelper();

    async function createPlannedCombat(
        request: Omit<CreatePlannedCombatRequest, "campaignId">,
        startImmidately: boolean
    ) {
        return await draftCombatHelper
            .createDraftCombat(
                {
                    ...request,
                    campaignId: route.params.campaignId,
                },
                startImmidately
            )
            .then(() => {
                modalState.modalOpen = false;
                toast.success("Created combat");
            });
    }

    async function openDraftCombat(
        request: Omit<OpenCombatRequest, "campaignId">
    ) {
        return await draftCombatHelper
            .openDraftCombat(route.params.campaignId, request.plannedCombatId)
            .then(() => {
                modalState.modalOpen = false;
                toast.success("Combat has been opened to players!");
            });
    }
</script>
