<template>
    <!-- The Combat tab's list (18c.3): Live, Drafts (DMs only) and Finished, newest first.
         Finished shows the last 20, then Show more. -->
    <div class="flex flex-col gap-5">
        <section
            v-for="section in sections"
            :key="section.key"
            :aria-labelledby="`${id}-${section.key}`"
            class="flex flex-col gap-2">
            <h2
                :id="`${id}-${section.key}`"
                class="text-sm font-semibold text-muted-foreground">
                {{ section.title }}
            </h2>
            <ul class="flex flex-col gap-2">
                <li
                    v-for="combat in section.items"
                    :key="combat.id">
                    <CombatListItem
                        :campaignId="campaignId"
                        :combat="combat" />
                </li>
            </ul>
            <Button
                v-if="
                    section.key === 'finished' &&
                    groups.finished.length > finishedShown
                "
                variant="ghost"
                class="h-11 self-center md:h-9"
                @click="finishedShown += FINISHED_PAGE_SIZE">
                Show more
            </Button>
        </section>
    </div>
</template>

<script setup lang="ts">
    import type { CombatSummary } from "~/utils/api/types";
    import { FINISHED_PAGE_SIZE, groupCombats } from "~/utils/combat";

    const props = defineProps<{
        campaignId: string;
        combats: readonly CombatSummary[];
        isDm: boolean;
    }>();

    const id = useId();
    const finishedShown = ref(FINISHED_PAGE_SIZE);
    const groups = computed(() => groupCombats(props.combats, props.isDm));
    const sections = computed(() =>
        [
            { key: "live", title: "Live", items: groups.value.live },
            { key: "drafts", title: "Drafts", items: groups.value.drafts },
            {
                key: "finished",
                title: "Finished",
                items: groups.value.finished.slice(0, finishedShown.value),
            },
        ].filter((s) => s.items.length > 0)
    );
</script>
