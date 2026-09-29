<template>
    <!-- One block of an article (the Summary, 25d), read (15f, design §4): ordinary
         markdown with chips; a secret block framed "🔒 DM" or "🔒 Me"; a quote as a
         blockquote with its source ("From Jo's note · Session 4 ↗", or "— Jo, Session 4 ↗"
         in the history). A quote of a 🔒 note is both. -->
    <div
        :id="`block-${block.id}`"
        :class="[
            'scroll-mt-4 transition-shadow duration-700',
            secret && 'rounded-md border border-dashed border-gold/50 bg-gold/5 px-3 pb-2 pt-1',
            highlighted && 'rounded-md ring-2 ring-gold/60 ring-offset-4 ring-offset-background',
        ]"
        :data-block-id="block.id">
        <p
            v-if="secret"
            class="mb-1 text-xs font-semibold text-gold"
            :title="audience">
            {{ secret }}
            <span class="sr-only">. {{ audience }}.</span>
        </p>
        <figure
            v-if="block.quote"
            class="flex flex-col gap-1">
            <blockquote class="border-l-2 border-gold/60 pl-3">
                <SessionNoteMarkdown
                    :campaignId="campaignId"
                    :text="block.text"
                    document />
            </blockquote>
            <!-- On the entry page (25d): a source chip that opens the note on the Notes
                 tab. Elsewhere (the history dialog): a link to the note in the stream. -->
            <figcaption
                v-if="sourceChip"
                class="pl-3">
                <button
                    type="button"
                    class="-my-1 inline-flex h-11 max-w-full items-center gap-1 rounded-full px-1 text-xs font-medium text-gold hover:underline md:my-0 md:h-7"
                    @click="emit('openNote', block.quote.noteId)">
                    <span class="truncate rounded-full border border-gold/40 bg-gold/5 px-2.5 py-1">
                        {{ sourceLabel }} ↗
                    </span>
                </button>
            </figcaption>
            <figcaption
                v-else
                class="pl-3 text-xs text-muted-foreground">
                — {{ nameOf(block.quote.authorMemberId) }},
                <NuxtLink
                    :to="noteHref"
                    class="-my-3 inline-flex min-h-11 items-center rounded font-medium text-gold hover:underline md:my-0 md:min-h-0"
                    :aria-label="`Session ${block.quote.sessionNumber}: open the quoted note`">
                    Session {{ block.quote.sessionNumber }} ↗
                </NuxtLink>
            </figcaption>
        </figure>
        <SessionNoteMarkdown
            v-else
            :campaignId="campaignId"
            :text="block.text"
            document />
    </div>
</template>

<script setup lang="ts">
    import type { ArticleBlock } from "~/utils/api/types";
    import { quoteSourceLabel, secretAudience, secretLabel } from "~/utils/article";
    import { NOTE_LINK_PARAM } from "~/utils/noteActions";

    const props = defineProps<{
        campaignId: string;
        block: ArticleBlock;
        viewerMemberId: string;
        nameOf: (memberId: string) => string;
        /** Opened from ⌘K (`?block=`, 17b): marked for a moment. */
        highlighted?: boolean;
        /** The entry page's source chip, which asks for the note on the Notes tab (25d). */
        sourceChip?: boolean;
    }>();
    const emit = defineEmits<{ openNote: [noteId: string] }>();

    const secret = computed(() => secretLabel(props.block.visibility));
    const audience = computed(() =>
        secretAudience(
            props.block.visibility,
            props.nameOf(props.block.ownerMemberId),
            props.block.ownerMemberId === props.viewerMemberId
        )
    );
    const sourceLabel = computed(() =>
        props.block.quote
            ? quoteSourceLabel(
                  props.nameOf(props.block.quote.authorMemberId),
                  props.block.quote.authorMemberId === props.viewerMemberId,
                  props.block.quote.sessionNumber
              )
            : ""
    );
    const noteHref = computed(() =>
        props.block.quote
            ? `/app/campaigns/${encodeURIComponent(props.campaignId)}?${NOTE_LINK_PARAM}=${encodeURIComponent(props.block.quote.noteId)}`
            : ""
    );
</script>
