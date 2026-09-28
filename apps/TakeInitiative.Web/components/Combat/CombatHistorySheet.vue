<template>
    <!-- A combat's history (18d.7, DMs): 18b's rows, newest first, each with who did it
         and when. A bottom sheet on a phone and a dialog from `md`. It is read again
         each time it opens. -->
    <DialogRoot v-model:open="open">
        <DialogPortal>
            <DialogOverlay
                class="fixed inset-0 z-50 bg-black/80 data-[state=closed]:animate-out data-[state=open]:animate-in data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
            <DialogContent
                class="fixed inset-x-0 bottom-0 z-50 flex max-h-[85dvh] flex-col rounded-t-xl border-t bg-background pb-safe shadow-lg outline-none data-[state=closed]:animate-out data-[state=open]:animate-in data-[state=closed]:slide-out-to-bottom data-[state=open]:slide-in-from-bottom md:inset-x-auto md:bottom-auto md:left-1/2 md:top-1/2 md:max-h-[85vh] md:w-full md:max-w-lg md:-translate-x-1/2 md:-translate-y-1/2 md:rounded-xl md:border md:pb-0 md:data-[state=closed]:slide-out-to-bottom-0 md:data-[state=open]:slide-in-from-bottom-0">
                <header
                    class="flex shrink-0 items-center gap-2 border-b py-1 pl-4 pr-1">
                    <DialogTitle
                        class="min-w-0 flex-1 truncate text-base font-semibold">
                        History
                        <span class="font-normal text-muted-foreground">
                            · {{ combatName }}</span
                        >
                    </DialogTitle>
                    <DialogDescription class="sr-only">
                        Everything that happened in this combat, newest first.
                    </DialogDescription>
                    <DialogClose
                        class="flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                        aria-label="Close">
                        <X
                            class="size-5"
                            aria-hidden="true" />
                    </DialogClose>
                </header>

                <div class="min-h-0 flex-1 overflow-y-auto overscroll-contain">
                    <LoadingFallback
                        v-if="!historyQuery.data.value"
                        :isLoading="historyQuery.isLoading.value"
                        :isError="historyQuery.isError.value"
                        iconSize="2x"
                        class="py-8" />
                    <p
                        v-else-if="rows.length === 0"
                        class="px-4 py-8 text-center text-sm text-muted-foreground">
                        Nothing has happened yet.
                    </p>
                    <ol
                        v-else
                        class="flex flex-col divide-y">
                        <li
                            v-for="row in rows"
                            :key="row.version"
                            class="flex gap-3 px-4 py-2.5">
                            <component
                                :is="KIND_ICONS[row.kind]"
                                class="mt-0.5 size-4 shrink-0 text-muted-foreground"
                                aria-hidden="true" />
                            <div class="flex min-w-0 flex-1 flex-col gap-0.5">
                                <p class="break-words text-sm">
                                    {{ row.text }}
                                </p>
                                <p class="text-xs text-muted-foreground">
                                    {{ actorName(row.actorMemberId) }} ·
                                    <time
                                        :datetime="row.timestamp"
                                        :title="
                                            formatNoteDateTime(row.timestamp)
                                        "
                                        >{{
                                            formatNoteTime(row.timestamp)
                                        }}</time
                                    >
                                </p>
                            </div>
                        </li>
                    </ol>
                </div>
            </DialogContent>
        </DialogPortal>
    </DialogRoot>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import {
        Dices,
        Flag,
        Pencil,
        SkipForward,
        Swords,
        UserMinus,
        UserPlus,
        X,
    } from "lucide-vue-next";
    import {
        DialogClose,
        DialogContent,
        DialogDescription,
        DialogOverlay,
        DialogPortal,
        DialogRoot,
        DialogTitle,
    } from "reka-ui";
    import type { Component } from "vue";
    import type { Campaign, CombatHistoryItem } from "~/utils/api/types";
    import { getCombatHistoryQuery } from "~/utils/queries/combats";
    import { formatNoteDateTime, formatNoteTime } from "~/utils/sessionDates";

    const props = defineProps<{
        campaign: Campaign;
        combatId: string;
        combatName: string;
    }>();
    const open = defineModel<boolean>("open", { required: true });

    const historyQuery = useQuery({
        ...getCombatHistoryQuery(
            () => props.campaign.id,
            () => props.combatId
        ),
        // Read again on every open (stale at once): the combat moves on while it is shut.
        enabled: () => open.value && !!props.combatId,
        staleTime: 0,
    });

    const rows = computed(() =>
        [...(historyQuery.data.value?.items ?? [])].reverse()
    );

    const usernames = computed(
        () =>
            new Map(
                props.campaign.members.map((m) => [
                    m.memberId.toLowerCase(),
                    m.username,
                ])
            )
    );
    const actorName = (memberId: string) =>
        usernames.value.get(memberId.toLowerCase()) ?? "A former member";

    const KIND_ICONS: Record<CombatHistoryItem["kind"], Component> = {
        Created: Swords,
        CombatantsAdded: UserPlus,
        CombatantEdited: Pencil,
        CombatantRemoved: UserMinus,
        InitiativeRolled: Dices,
        TurnEnded: SkipForward,
        Finished: Flag,
    };
</script>
