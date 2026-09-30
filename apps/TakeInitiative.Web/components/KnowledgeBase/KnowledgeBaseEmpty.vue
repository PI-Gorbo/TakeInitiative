<template>
    <!-- The two empty states (26f), and the difference between them is the point of this
         page. Step 21's corpus lived only inside ⌘K: an unset `IndexPath` made the whole
         provider vanish, and nothing in the UI was ever responsible for saying that nothing
         had been ingested. Now something is.

         "No reference material yet" is not an error and names no file path — the person
         reading it is usually not the operator, and a path they cannot act on would only
         look like a broken install. -->
    <EmptyState
        :icon="state === 'notIngested' ? Library : SearchX"
        :title="copy.title">
        <!-- "No items match. Clear filters." — the second sentence is the button, not a
             line of text above it, so the words are not said twice. -->
        <template v-if="state === 'notIngested'">{{ copy.detail }}</template>
        <span
            v-else
            class="block">
            <Button
                variant="outline"
                class="h-11 md:h-9"
                @click="emit('clear')">
                {{ clearLabel }}
            </Button>
        </span>
    </EmptyState>
</template>

<script setup lang="ts">
    import { Library, SearchX } from "lucide-vue-next";
    import { KNOWLEDGE_BASE_EMPTY, type KnowledgeBaseEmpty } from "~/utils/knowledgeBase";

    const props = defineProps<{ state: KnowledgeBaseEmpty }>();
    const emit = defineEmits<{ clear: [] }>();

    const copy = computed(() => KNOWLEDGE_BASE_EMPTY[props.state]);
    // The button says what the copy's second sentence says, without its full stop.
    const clearLabel = KNOWLEDGE_BASE_EMPTY.noMatch.detail.replace(/\.$/, "");
</script>
