<template>
    <!-- The Join combat banner (18e.3): above the stream while a combat the viewer can
         see is live. It names the newest live combat and says how many more there are. -->
    <div
        v-if="banner"
        class="flex shrink-0 items-center gap-2 border-b border-gold/40 bg-gold/10 px-3 py-1 text-sm"
        role="status">
        <p class="min-w-0 flex-1 truncate">
            <span aria-hidden="true">⚔ </span>
            <span class="font-semibold">{{ banner.combat.name }}</span>
            is live
            <span class="text-muted-foreground">· Round {{ banner.combat.round }}</span>
            <span
                v-if="banner.more > 0"
                class="text-muted-foreground">
                · +{{ banner.more }} more</span
            >
        </p>
        <NuxtLink
            v-if="banner.more > 0"
            :to="`/app/campaigns/${encodeURIComponent(campaignId)}/combat`"
            class="hidden h-11 shrink-0 items-center rounded-md px-2 text-xs text-muted-foreground hover:bg-accent hover:text-accent-foreground sm:flex md:h-9">
            All combats
        </NuxtLink>
        <NuxtLink
            :to="`/app/campaigns/${encodeURIComponent(campaignId)}/combat/${encodeURIComponent(banner.combat.id)}`"
            class="flex h-11 shrink-0 items-center gap-1 rounded-md bg-gold px-3 text-sm font-semibold text-gold-foreground hover:bg-gold/90 md:h-9">
            <Swords
                class="size-4"
                aria-hidden="true" />
            Join combat
        </NuxtLink>
    </div>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { Swords } from "lucide-vue-next";
    import { liveCombatBanner } from "~/utils/combatCard";
    import { getCombatsQuery } from "~/utils/queries/combats";

    const props = defineProps<{ campaignId: string }>();

    // The live combats, which pushes keep current (18c.2).
    const liveQuery = useQuery(getCombatsQuery(() => props.campaignId, { status: ["Active"] }));
    const banner = computed(() => liveCombatBanner(liveQuery.data.value?.combats));
</script>
