<template>
    <!-- One block of an article, read (15f, design §4): ordinary markdown with chips; a
         secret block framed "🔒 DM" or "🔒 Me"; a quote as a blockquote with
         "— Sam, Session 12 ↗" linking back to its note. A quote of a 🔒 note is both. -->
    <div
        :class="[secret && 'rounded-md border border-dashed border-gold/50 bg-gold/5 px-3 pb-2 pt-1']"
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
            <figcaption class="pl-3 text-xs text-muted-foreground">
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
    import { secretAudience, secretLabel } from "~/utils/article";
    import { NOTE_LINK_PARAM } from "~/utils/noteActions";

    const props = defineProps<{
        campaignId: string;
        block: ArticleBlock;
        viewerMemberId: string;
        nameOf: (memberId: string) => string;
    }>();

    const secret = computed(() => secretLabel(props.block.visibility));
    const audience = computed(() =>
        secretAudience(
            props.block.visibility,
            props.nameOf(props.block.ownerMemberId),
            props.block.ownerMemberId === props.viewerMemberId
        )
    );
    const noteHref = computed(() =>
        props.block.quote
            ? `/app/campaigns/${encodeURIComponent(props.campaignId)}?${NOTE_LINK_PARAM}=${encodeURIComponent(props.block.quote.noteId)}`
            : ""
    );
</script>
