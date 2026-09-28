<template>
    <!-- The stat-block card (20c, glossary "Stat block"): a reference monster laid out the way
         SRD 5.2 lays it out, read-only. Every word is a text node; the pure parts are in
         `utils/reference.ts`. The `actions` slot sits under the name (20d's + Wiki). -->
    <article
        class="flex flex-col gap-3 rounded-lg border bg-card p-4 text-card-foreground md:p-6"
        :aria-label="`${block.name} stat block`">
        <header class="flex flex-col gap-0.5">
            <h1 class="font-serif text-2xl font-bold leading-tight md:text-3xl">{{ block.name }}</h1>
            <p class="text-sm italic text-muted-foreground">{{ typeLine(block) }}</p>
            <div
                class="mt-2 h-0.5 rounded-full bg-gold"
                aria-hidden="true" />
            <div
                v-if="$slots.actions"
                class="mt-2 flex flex-wrap gap-2">
                <slot name="actions" />
            </div>
        </header>

        <div class="flex flex-col gap-0.5 text-sm">
            <p>
                <strong class="font-semibold text-gold">AC</strong> {{ block.ac }}
                <!-- The spaces are inside the span: Vue drops the newline between it and
                     <strong>, which left "AC 15 ·Initiative". -->
                <span
                    class="text-muted-foreground"
                    aria-hidden="true"> · </span>
                <strong class="font-semibold text-gold">Initiative</strong> {{ initiativeLine(block.initiativeBonus) }}
            </p>
            <p><strong class="font-semibold text-gold">HP</strong> {{ hpLine(block) }}</p>
            <p><strong class="font-semibold text-gold">Speed</strong> {{ speedLine(block.speed) }}</p>
        </div>

        <!-- 5.2's ability table, with its MOD and SAVE columns: three across on a phone
             (STR DEX CON over INT WIS CHA), six from md. -->
        <div class="grid grid-cols-3 gap-1.5 md:grid-cols-6">
            <div
                v-for="row in abilities"
                :key="row.key"
                role="group"
                :aria-label="`${ABILITY_NAMES[row.key]} ${row.score}, modifier ${row.mod}, save ${row.save}`"
                class="flex flex-col items-center rounded-md bg-muted/60 px-1 py-1.5 tabular-nums">
                <span
                    class="text-xs font-semibold tracking-wide text-gold"
                    aria-hidden="true">{{ row.label }}</span>
                <span
                    class="text-lg font-semibold leading-tight"
                    aria-hidden="true">{{ row.score }}</span>
                <span
                    class="grid w-full grid-cols-2 text-center text-xs"
                    aria-hidden="true">
                    <span class="text-[0.625rem] uppercase text-muted-foreground">Mod</span>
                    <span class="text-[0.625rem] uppercase text-muted-foreground">Save</span>
                    <span>{{ row.mod }}</span>
                    <span>{{ row.save }}</span>
                </span>
            </div>
        </div>

        <div class="flex flex-col gap-0.5 text-sm">
            <p
                v-for="line in details"
                :key="line.label">
                <strong class="font-semibold text-gold">{{ line.label }}</strong> {{ line.text }}
            </p>
            <p><strong class="font-semibold text-gold">CR</strong> {{ crLine(block) }}</p>
        </div>

        <ReferenceStatBlockTraits
            v-for="section in sections"
            :key="section.key"
            :section="section" />
    </article>
</template>

<script setup lang="ts">
    import type { StatBlock } from "~/utils/api/types";
    import {
        abilityRows,
        crLine,
        detailLines,
        hpLine,
        initiativeLine,
        speedLine,
        statBlockSections,
        typeLine,
        type Ability,
    } from "~/utils/reference";

    const props = defineProps<{ block: StatBlock }>();

    const ABILITY_NAMES: Record<Ability, string> = {
        str: "Strength",
        dex: "Dexterity",
        con: "Constitution",
        int: "Intelligence",
        wis: "Wisdom",
        cha: "Charisma",
    };

    const abilities = computed(() => abilityRows(props.block));
    const details = computed(() => detailLines(props.block));
    const sections = computed(() => statBlockSections(props.block));
</script>
