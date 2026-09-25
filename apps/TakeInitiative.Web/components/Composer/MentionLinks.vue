<template>
    <!-- What the text's `@[…]` mentions link to (15d). The text box cannot draw chips,
         so this row shows them: new entries (Create) are marked "new", and a click on
         one changes its kind. Hidden while the `@` suggestions are open. -->
    <div
        v-if="links.length > 0"
        class="flex items-center gap-1 overflow-x-auto text-xs text-muted-foreground"
        aria-label="Mentions">
        <template
            v-for="link in links"
            :key="link.entryId">
            <button
                v-if="link.isNew"
                type="button"
                class="flex h-7 shrink-0 items-center gap-1 whitespace-nowrap rounded-md bg-gold/10 px-1.5 text-gold hover:bg-gold/20"
                :title="`New ${link.entryKind}, created when the note is posted. Click to change its kind.`"
                @mousedown.prevent
                @click="cycleKind(link.entryId)">
                <span aria-hidden="true">{{ ENTRY_KIND_ICONS[link.entryKind] }}</span>
                {{ link.name }}
                <span class="rounded bg-gold/15 px-1">new</span>
            </button>
            <span
                v-else
                class="flex h-7 shrink-0 items-center gap-1 whitespace-nowrap rounded-md bg-muted px-1.5">
                <span aria-hidden="true">{{ ENTRY_KIND_ICONS[link.entryKind] }}</span>
                {{ link.name }}
            </span>
        </template>
    </div>
</template>

<script setup lang="ts">
    import { ENTRY_KINDS, ENTRY_KIND_ICONS, type EntryDirectory } from "~/utils/entries";
    import { linkedMentions, type MentionText } from "~/utils/mentions";

    const props = defineProps<{ state: MentionText; directory: EntryDirectory }>();

    const links = computed(() => linkedMentions(props.state, props.directory));

    function cycleKind(entryId: string) {
        const kinds = ENTRY_KINDS.map((k) => k.value);
        // eslint-disable-next-line vue/no-mutating-props -- the caller's reactive text state
        props.state.newEntries = props.state.newEntries.map((e) =>
            e.id === entryId ? { ...e, kind: kinds[(kinds.indexOf(e.kind) + 1) % kinds.length]! } : e
        );
    }
</script>
