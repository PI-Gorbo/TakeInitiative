<template>
    <!-- One section of a stat block (20c): Traits, Actions, Bonus Actions, Reactions or
         Legendary Actions. Each row is a bold italic name, then its text. The text is only
         ever text nodes: `utils/reference.ts` splits its bold, italics, paragraphs and lists. -->
    <section class="flex flex-col gap-2">
        <h3 class="border-b border-gold/60 pb-0.5 font-serif text-lg font-semibold text-gold">
            {{ section.title }}
        </h3>
        <div
            v-for="(row, index) in rows"
            :key="index"
            class="flex flex-col gap-1.5 text-sm leading-relaxed">
            <template
                v-for="(block, b) in row.blocks"
                :key="b">
                <p v-if="block.type === 'paragraph'">
                    <strong
                        v-if="b === 0"
                        class="font-semibold italic">{{ row.name }}.</strong>{{ b === 0 ? " " : "" }}<template
                        v-for="(run, r) in block.runs"
                        :key="r"><strong
                            v-if="run.bold"
                            class="font-semibold">{{ run.text }}</strong><em v-else-if="run.italic">{{ run.text }}</em><template v-else>{{ run.text }}</template></template>
                </p>
                <template v-else>
                    <p
                        v-if="b === 0"
                        class="font-semibold italic">
                        {{ row.name }}.
                    </p>
                    <ul class="flex list-disc flex-col gap-1 pl-5">
                        <li
                            v-for="(item, i) in block.items"
                            :key="i">
                            <template
                                v-for="(run, r) in item"
                                :key="r"><strong
                                    v-if="run.bold"
                                    class="font-semibold">{{ run.text }}</strong><em v-else-if="run.italic">{{ run.text }}</em><template v-else>{{ run.text }}</template></template>
                        </li>
                    </ul>
                </template>
            </template>
        </div>
    </section>
</template>

<script setup lang="ts">
    import { textBlocks, type StatBlockSection } from "~/utils/reference";

    const props = defineProps<{ section: StatBlockSection }>();

    const rows = computed(() =>
        props.section.rows.map((row) => {
            const blocks = textBlocks(row.text);
            // A row with no text still shows its name.
            return { name: row.name, blocks: blocks.length ? blocks : [{ type: "paragraph" as const, runs: [] }] };
        })
    );
</script>
