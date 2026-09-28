<template>
    <!-- A combat card (glossary, design §3, 18e): the combat drawn in its session in the
         stream. "⚔ Combat · Goblin Ambush · 3 rounds · @Klarg, 4× @Goblin", with a Live
         badge while it runs, and Open. It holds only the viewer's own view of the combat. -->
    <article
        :id="`combat-${card.id}`"
        :aria-label="`Combat: ${card.name}`"
        class="px-4 py-1.5">
        <div
            :class="[
                'flex items-start gap-3 rounded-md border px-3 py-2',
                card.status === 'Active' ? 'border-gold/60 bg-gold/5' : 'bg-muted/30',
            ]">
            <div class="flex min-w-0 flex-1 flex-col gap-1 text-sm">
                <p class="flex flex-wrap items-baseline gap-x-1.5 gap-y-0.5">
                    <span
                        v-if="sessionNumber !== undefined"
                        class="font-semibold text-gold"
                        :aria-label="`Session ${sessionNumber}`"
                        >S{{ sessionNumber }}</span
                    >
                    <span
                        v-if="sessionNumber !== undefined"
                        aria-hidden="true"
                        class="text-muted-foreground"
                        >·</span
                    >
                    <span class="font-semibold"><span aria-hidden="true">⚔ </span>Combat</span>
                    <span
                        aria-hidden="true"
                        class="text-muted-foreground"
                        >·</span
                    >
                    <span class="min-w-0 break-words font-medium">{{ card.name }}</span>
                    <span
                        aria-hidden="true"
                        class="text-muted-foreground"
                        >·</span
                    >
                    <span class="text-muted-foreground">{{ cardRoundsLabel(card) }}</span>
                    <span
                        v-if="card.status === 'Active'"
                        class="self-center rounded-full bg-gold px-1.5 text-xs font-semibold text-gold-foreground"
                        >Live</span
                    >
                </p>
                <p
                    v-if="card.combatants.length > 0"
                    class="flex flex-wrap items-baseline gap-x-1 gap-y-1">
                    <template
                        v-for="(row, index) in rows"
                        :key="index">
                        <span class="inline-flex items-baseline">
                            {{ cardCountPrefix(row) }}
                            <NuxtLink
                                v-if="row.href"
                                :to="row.href"
                                class="ml-0.5 rounded-md bg-gold/10 px-1.5 font-medium text-gold no-underline hover:bg-gold/20 hover:underline focus-visible:bg-gold/20"
                                >@{{ row.label }}</NuxtLink
                            >
                            <span v-else>{{ row.label }}</span
                            ><span
                                v-if="index < rows.length - 1"
                                aria-hidden="true"
                                >,</span
                            >
                        </span>
                    </template>
                </p>
                <p
                    v-else
                    class="text-xs text-muted-foreground">
                    No combatants
                </p>
            </div>
            <NuxtLink
                :to="combatHref"
                class="-my-1 flex h-11 shrink-0 items-center gap-1 rounded-md px-3 text-sm font-medium text-gold hover:bg-accent md:h-9"
                :aria-label="`Open ${card.name}`">
                Open
                <ChevronRight
                    class="size-4"
                    aria-hidden="true" />
            </NuxtLink>
        </div>
    </article>
</template>

<script setup lang="ts">
    import { ChevronRight } from "lucide-vue-next";
    import type { CombatCard } from "~/utils/api/types";
    import { entryHref } from "~/utils/article";
    import { cardCountPrefix, cardRoundsLabel } from "~/utils/combatCard";
    import type { EntryDirectory } from "~/utils/entries";
    import { resolveEntry } from "~/utils/entries";

    const props = defineProps<{
        campaignId: string;
        card: CombatCard;
        /** The viewer's entry directory: an entry the card links is named as the wiki names it now. */
        directory: EntryDirectory;
        /** On an entry page (§4's "Goblin Ambush (S12)"): the session the combat is in. */
        sessionNumber?: number;
    }>();

    const combatHref = computed(
        () => `/app/campaigns/${encodeURIComponent(props.campaignId)}/combat/${encodeURIComponent(props.card.id)}`
    );

    // An entry the directory knows is a mention chip under its current name; any other
    // line (a plain name, or an entry the directory has not loaded) is text.
    const rows = computed(() =>
        props.card.combatants.map((row) => {
            const entry = row.entryId ? resolveEntry(props.directory, row.entryId) : undefined;
            return {
                count: row.count,
                label: entry?.name ?? row.name,
                href: entry ? entryHref(props.campaignId, entry.id) : undefined,
            };
        })
    );
</script>
