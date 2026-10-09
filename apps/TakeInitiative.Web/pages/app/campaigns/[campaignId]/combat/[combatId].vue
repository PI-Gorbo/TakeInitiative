<template>
    <div class="flex min-h-full w-full flex-col">
        <!-- A combat (18c.4, design §8, mobile first): the header with the DM's actions,
             the initiative order, the waiting combatants, and the End turn bar at the
             bottom. Every write and push lands in the cache (18c.6). Tapping a combatant
             opens its sheet (18d), and a DM drags rows to reorder and opens the history.
             ✎ in the bar opens the slim composer (18e). -->
        <PageContainer class="flex flex-1 flex-col gap-4 px-3 py-3 md:px-4">
            <NuxtLink
                :to="`/app/campaigns/${encodeURIComponent(campaignId)}/combat`"
                class="-ml-2 flex h-11 w-fit items-center gap-1 rounded-md px-2 text-sm text-muted-foreground hover:bg-accent hover:text-accent-foreground md:h-9">
                <ChevronLeft
                    class="size-4"
                    aria-hidden="true" />
                Combats
            </NuxtLink>

            <div
                v-if="notFound"
                class="flex flex-col items-center">
                <EmptyState
                    :icon="Swords"
                    title="This combat isn't available.">
                    It may not have started yet, or it is not in this campaign.
                </EmptyState>
                <NuxtLink
                    :to="`/app/campaigns/${encodeURIComponent(campaignId)}/combat`"
                    class="-mt-10 flex h-11 items-center font-medium text-gold underline underline-offset-2">
                    See all combats
                </NuxtLink>
            </div>
            <LoadingFallback
                v-else-if="!combat || !campaign"
                :isLoading="combatQuery.isLoading.value || !campaign"
                :isError="combatQuery.isError.value"
                iconSize="2x"
                class="pt-8" />
            <template v-else>
                <header class="flex flex-col gap-3">
                    <div class="flex min-w-0 items-start gap-2">
                        <div class="flex min-w-0 flex-1 flex-col gap-1">
                            <h2 class="break-words text-xl font-semibold">
                                {{ combat.name }}
                            </h2>
                            <p
                                class="flex items-center gap-2 text-sm text-muted-foreground">
                                <span
                                    :class="[
                                        'rounded-full px-2 py-0.5 text-xs font-semibold',
                                        combat.status === 'Active'
                                            ? 'bg-gold text-gold-foreground'
                                            : 'border text-muted-foreground',
                                    ]"
                                    >{{ combatStatusLabel(combat) }}</span
                                >
                                <span>{{
                                    combatantCountLabel(
                                        combat.combatants.length
                                    )
                                }}</span>
                            </p>
                        </div>
                        <DropdownMenu v-if="viewer.isDm">
                            <DropdownMenuTrigger
                                aria-label="More combat actions"
                                class="flex size-11 shrink-0 items-center justify-center rounded-md border hover:bg-accent hover:text-accent-foreground md:size-9">
                                <MoreHorizontal
                                    class="size-4"
                                    aria-hidden="true" />
                            </DropdownMenuTrigger>
                            <DropdownMenuContent
                                align="end"
                                class="w-48">
                                <DropdownMenuItem
                                    class="min-h-11 gap-2 md:min-h-8"
                                    @select="historyOpen = true">
                                    <History
                                        class="size-4"
                                        aria-hidden="true" />
                                    History
                                </DropdownMenuItem>
                                <DropdownMenuItem
                                    v-if="actions.finish"
                                    class="min-h-11 gap-2 md:min-h-8"
                                    @select="finishOpen = true">
                                    <Flag
                                        class="size-4"
                                        aria-hidden="true" />
                                    {{
                                        combat.status === "Draft"
                                            ? "Discard draft"
                                            : "Finish combat"
                                    }}
                                </DropdownMenuItem>
                            </DropdownMenuContent>
                        </DropdownMenu>
                    </div>

                    <div
                        v-if="
                            actions.add ||
                            actions.roll ||
                            actions.addMyCharacter ||
                            actions.rollMine
                        "
                        class="flex flex-wrap gap-2">
                        <Button
                            v-if="actions.add"
                            variant="outline"
                            class="h-11 gap-2 md:h-9"
                            @click="addOpen = true">
                            <UserPlus
                                class="size-4"
                                aria-hidden="true" />
                            Add
                        </Button>
                        <CombatAddMyCharacter
                            v-if="actions.addMyCharacter"
                            :campaignId="campaignId"
                            :combat="combat"
                            :memberId="viewer.memberId" />
                        <Button
                            v-if="actions.roll"
                            class="h-11 gap-2 md:h-9"
                            :disabled="roll.isPending.value"
                            @click="rollInitiative">
                            <Dices
                                class="size-4"
                                aria-hidden="true" />
                            {{ actions.roll }}
                        </Button>
                        <Button
                            v-if="actions.rollMine"
                            class="h-11 gap-2 md:h-9"
                            :disabled="roll.isPending.value"
                            @click="rollInitiative">
                            <Dices
                                class="size-4"
                                aria-hidden="true" />
                            Roll my initiative
                        </Button>
                    </div>
                </header>

                <CombatInitiativeList
                    :campaignId="campaignId"
                    :combatants="split.ordered"
                    :turnCombatantId="combat.turnCombatantId"
                    :isDm="viewer.isDm"
                    :viewerMemberId="viewer.memberId"
                    :canOpen="canOpen"
                    :reorderable="viewer.isDm && combat.status !== 'Finished'"
                    @open="(id) => (sheetCombatantId = id)"
                    @reorder="reorderCombatant">
                    <template #empty>
                        {{ emptyOrderText }}
                    </template>
                </CombatInitiativeList>

                <CombatWaitingList
                    :campaignId="campaignId"
                    :combatants="split.waiting"
                    :canOpen="canOpen"
                    @open="(id) => (sheetCombatantId = id)">
                    <template #action>
                        <Button
                            v-if="actions.roll || actions.rollMine"
                            variant="ghost"
                            class="h-11 gap-2 md:h-8"
                            :disabled="roll.isPending.value"
                            @click="rollInitiative">
                            <Dices
                                class="size-4"
                                aria-hidden="true" />
                            {{ actions.roll ?? "Roll my initiative" }}
                        </Button>
                    </template>
                </CombatWaitingList>
            </template>
        </PageContainer>

        <!-- The bar, and the slim composer above it (18e.5, design §3): a session note
             from the fight, posted to the current session and not tied to the combat. -->
        <div
            v-if="combat && campaign"
            class="sticky bottom-0 z-10">
            <Composer
                v-if="composing"
                ref="composer"
                slim
                :campaign="campaign"
                @posted="onPosted" />
            <CombatBar
                :state="actions.endTurn"
                :pending="endTurn.isPending.value"
                :composing="composing"
                @endTurn="endCurrentTurn"
                @compose="toggleComposer" />
        </div>

        <CombatAddCombatantsDialog
            v-if="combat && viewer.isDm"
            v-model:open="addOpen"
            :campaignId="campaignId"
            :combatId="combatId" />

        <CombatCombatantSheet
            v-if="combat"
            v-model:combatantId="sheetCombatantId"
            :campaignId="campaignId"
            :combat="combat"
            :viewer="viewer"
            @reorder="reorderCombatant" />

        <CombatHistorySheet
            v-if="combat && campaign && viewer.isDm"
            v-model:open="historyOpen"
            :campaign="campaign"
            :combatId="combat.id"
            :combatName="combat.name" />

        <Dialog v-model:open="finishOpen">
            <DialogContent class="max-w-sm">
                <DialogHeader>
                    <DialogTitle>{{
                        combat?.status === "Draft"
                            ? "Discard this draft?"
                            : "Finish this combat?"
                    }}</DialogTitle>
                    <DialogDescription>
                        {{
                            combat?.status === "Draft"
                                ? "It never started, so only the DMs will see it, under Finished."
                                : "Nobody can change it after this. It stays in its session for everyone to read."
                        }}
                    </DialogDescription>
                </DialogHeader>
                <DialogFooter class="gap-2">
                    <Button
                        variant="ghost"
                        class="h-11 md:h-9"
                        @click="finishOpen = false">
                        Cancel
                    </Button>
                    <Button
                        variant="destructive"
                        class="h-11 md:h-9"
                        :disabled="finish.isPending.value"
                        @click="finishCombat">
                        {{ combat?.status === "Draft" ? "Discard" : "Finish" }}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    </div>
</template>

<script setup lang="ts">
    import { useQuery, useQueryClient } from "@tanstack/vue-query";
    import {
        ChevronLeft,
        Dices,
        Flag,
        History,
        MoreHorizontal,
        Swords,
        UserPlus,
    } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import { apiErrorMessage, apiErrorStatus } from "~/utils/apiErrorParser";
    import { currentMember } from "~/utils/campaign";
    import type { Combat, Combatant } from "~/utils/api/types";
    import {
        canOpenSheet,
        combatActions,
        combatStatusLabel,
        combatantCountLabel,
        splitCombatants,
        withMovedCombatant,
    } from "~/utils/combat";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import {
        endTurnMutation,
        finishCombatMutation,
        getCombatQuery,
        getCombatQueryKey,
        putCombatantPositionMutation,
        rollCombatMutation,
    } from "~/utils/queries/combats";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });

    const route = useRoute("app-campaigns-campaignId-combat-combatId");
    const campaignId = computed(() => route.params.campaignId as string);
    const combatId = computed(() => route.params.combatId as string);

    const campaignQuery = useQuery(getCampaignQuery(campaignId));
    const campaign = computed(() => campaignQuery.data.value);
    const combatQuery = useQuery(getCombatQuery(campaignId, combatId));
    const combat = computed(() => combatQuery.data.value);
    const notFound = computed(
        () => apiErrorStatus(combatQuery.error.value) === 404
    );

    useHead({
        title: () =>
            combat.value ? `${combat.value.name} · Combat` : "Combat",
    });

    const viewer = computed(() => ({
        memberId: campaign.value?.currentMemberId ?? "",
        isDm: currentMember(campaign.value)?.role === "DM",
    }));
    const actions = computed(() =>
        combat.value
            ? combatActions(combat.value, viewer.value)
            : {
                  add: false,
                  roll: null,
                  finish: false,
                  addMyCharacter: false,
                  rollMine: false,
                  endTurn: null,
              }
    );
    const split = computed(() =>
        splitCombatants(combat.value ?? { combatants: [] })
    );

    const emptyOrderText = computed(() => {
        if (!combat.value) return "";
        if (combat.value.status === "Draft")
            return combat.value.combatants.length === 0
                ? "Add combatants, then start the combat to roll initiative."
                : "Start the combat to roll everyone's initiative.";
        if (combat.value.status === "Finished")
            return "Nobody rolled initiative in this combat.";
        return "Nobody has rolled initiative yet.";
    });

    // ── The slim composer (18e.5) ────────────────────────────────────────────
    const composing = ref(false);
    const composer = useTemplateRef<{ focus: () => void }>("composer");
    function toggleComposer() {
        composing.value = !composing.value;
        if (composing.value) void nextTick(() => composer.value?.focus());
    }
    function onPosted() {
        composing.value = false;
        toast.success("Note posted to the session.");
    }

    const addOpen = ref(false);
    const finishOpen = ref(false);
    const historyOpen = ref(false);
    /** The combatant whose sheet is open (18d). */
    const sheetCombatantId = ref<string | null>(null);
    const canOpen = (combatant: Combatant) =>
        !!combat.value && canOpenSheet(combat.value, combatant, viewer.value);
    // A role change (a push, then a refetch) takes the DM's dialogs away.
    watch(
        () => viewer.value.isDm,
        (isDm) => {
            if (!isDm) {
                addOpen.value = false;
                finishOpen.value = false;
                historyOpen.value = false;
            }
        }
    );

    const roll = rollCombatMutation();
    async function rollInitiative() {
        try {
            await roll.mutateAsync({
                campaignId: campaignId.value,
                combatId: combatId.value,
            });
        } catch (err) {
            toast.error(apiErrorMessage(err, "Could not roll initiative."));
        }
    }

    const endTurn = endTurnMutation();
    async function endCurrentTurn() {
        const current = combat.value;
        const turnId = actions.value.endTurn?.combatantId;
        if (!current || !turnId) return;
        try {
            await endTurn.mutateAsync({
                campaignId: campaignId.value,
                combatId: current.id,
                combatantId: turnId,
                round: current.round,
            });
        } catch (err) {
            toast.error(apiErrorMessage(err, "Could not end the turn."));
        }
    }

    // Drag, the handle's arrow keys, or Move up / Move down (18d.6). The row stays where
    // it was dropped while the server places it.
    const queryClient = useQueryClient();
    const position = putCombatantPositionMutation();
    async function reorderCombatant(
        combatantId: string,
        afterId: string | null
    ) {
        const key = getCombatQueryKey(campaignId.value, combatId.value);
        queryClient.setQueryData<Combat>(
            key,
            (old) => old && withMovedCombatant(old, combatantId, afterId)
        );
        try {
            await position.mutateAsync({
                campaignId: campaignId.value,
                combatId: combatId.value,
                combatantId,
                afterId,
            });
        } catch (err) {
            toast.error(apiErrorMessage(err, "Could not move the combatant."));
            void queryClient.invalidateQueries({ queryKey: key });
        }
    }

    const finish = finishCombatMutation();
    async function finishCombat() {
        try {
            await finish.mutateAsync({
                campaignId: campaignId.value,
                combatId: combatId.value,
            });
            finishOpen.value = false;
        } catch (err) {
            toast.error(apiErrorMessage(err, "Could not finish the combat."));
        }
    }
</script>
