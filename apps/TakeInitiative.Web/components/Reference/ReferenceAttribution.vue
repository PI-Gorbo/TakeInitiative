<template>
    <!-- The source's attribution under every stat-block card (20c; CC-BY-4.0 asks for it).
         The words are the API's (`source.json`), with its URLs as links; the web holds no copy. -->
    <p class="text-xs leading-relaxed text-muted-foreground">
        <template
            v-for="(part, index) in parts"
            :key="index"><a
                v-if="part.href"
                :href="part.href"
                target="_blank"
                rel="noopener noreferrer"
                class="break-all underline underline-offset-2 hover:text-foreground">{{ part.text }}</a><template v-else>{{ part.text }}</template></template>
    </p>
</template>

<script setup lang="ts">
    import type { ReferenceAttribution } from "~/utils/api/types";
    import { attributionParts } from "~/utils/reference";

    const props = defineProps<{ attribution: ReferenceAttribution }>();

    const parts = computed(() => attributionParts(props.attribution.text));
</script>
