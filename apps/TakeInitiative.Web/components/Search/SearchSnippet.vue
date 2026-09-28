<template>
    <!-- A snippet (17a): plain text with the matches in <mark>, as text nodes, never
         as HTML. -->
    <span class="line-clamp-2 break-words text-sm text-muted-foreground"
        ><template
            v-for="(segment, index) in segments"
            :key="index"
            ><mark
                v-if="segment.mark"
                class="rounded-sm bg-gold/25 px-0.5 text-foreground"
                >{{ segment.text }}</mark
            ><template v-else>{{ segment.text }}</template></template
        ></span
    >
</template>

<script setup lang="ts">
    import type { Snippet } from "~/utils/api/types";
    import { snippetSegments } from "~/utils/search";

    const props = defineProps<{ snippet: Snippet }>();
    const segments = computed(() => snippetSegments(props.snippet));
</script>
