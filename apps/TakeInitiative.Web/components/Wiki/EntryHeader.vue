<template>
    <!-- An entry's header (design §4, 15c, 25e): its kind, name and "aka …" (the first three,
         a tap shows the rest), a 🔒 badge for DM and Me, and a player character's "Played by"
         chip. Its primary image (SAM-12) sits left of the kind and name, and opens the gallery's
         viewer; it falls back to nothing if the bytes will not load. The ⋯ menu holds Edit details
         (for someone who can change something), Set primary image (for editors), History (for
         everyone) and Merge into… (for editors). Edit access now lives in Details. -->
    <header class="flex flex-col gap-1.5">
        <div class="flex items-start gap-2">
            <button
                v-if="entry.primaryImageId && !imageFailed"
                type="button"
                class="size-14 shrink-0 overflow-hidden rounded-md bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                :aria-label="`Open the primary image of ${entry.name}`"
                @click="openPrimaryImage">
                <img
                    :src="src(campaignId, entry.primaryImageId, 'thumb')"
                    :alt="`${entry.name}'s primary image`"
                    class="size-full object-cover"
                    decoding="async"
                    @error="imageFailed = true" />
            </button>
            <div class="flex min-w-0 flex-1 flex-col gap-0.5">
                <p class="flex items-center gap-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    <span aria-hidden="true">{{ ENTRY_KIND_ICONS[entry.kind] }}</span>
                    {{ entry.kind }}
                    <span
                        v-if="entry.visibility !== 'Everyone'"
                        class="ml-1 rounded border px-1.5 font-medium normal-case"
                        :title="entry.visibility === 'DM' ? 'Visible to the DMs and the creator' : 'Visible only to the creator'">
                        🔒 {{ entry.visibility }}
                    </span>
                </p>
                <h2 class="break-words text-2xl font-semibold leading-tight">
                    {{ entry.name }}
                </h2>
            </div>
            <DropdownMenu>
                <DropdownMenuTrigger
                    aria-label="More actions"
                    class="flex size-11 shrink-0 items-center justify-center rounded-md border hover:bg-accent hover:text-accent-foreground md:size-9">
                    <MoreHorizontal
                        class="size-4"
                        aria-hidden="true" />
                </DropdownMenuTrigger>
                <DropdownMenuContent
                    align="end"
                    class="w-48">
                    <DropdownMenuItem
                        v-if="canEdit || canChangeAccess"
                        class="min-h-11 gap-2 md:min-h-8"
                        @select="emit('edit')">
                        <Pencil
                            class="size-4"
                            aria-hidden="true" />
                        Edit details
                    </DropdownMenuItem>
                    <DropdownMenuItem
                        v-if="canEdit"
                        class="min-h-11 gap-2 md:min-h-8"
                        @select="emit('primaryImage')">
                        <ImageIcon
                            class="size-4"
                            aria-hidden="true" />
                        {{ entry.primaryImageId ? "Change primary image" : "Set primary image" }}
                    </DropdownMenuItem>
                    <DropdownMenuItem
                        class="min-h-11 gap-2 md:min-h-8"
                        @select="emit('history')">
                        <History
                            class="size-4"
                            aria-hidden="true" />
                        History
                    </DropdownMenuItem>
                    <DropdownMenuItem
                        v-if="canEdit"
                        class="min-h-11 gap-2 md:min-h-8"
                        @select="emit('merge')">
                        <Merge
                            class="size-4"
                            aria-hidden="true" />
                        Merge into…
                    </DropdownMenuItem>
                </DropdownMenuContent>
            </DropdownMenu>
        </div>
        <p
            v-if="entry.aliases.length > 0"
            class="break-words text-sm text-muted-foreground">
            {{ aliases.label }}
            <button
                v-if="aliases.hidden > 0"
                type="button"
                class="-my-3 ml-1 inline-flex min-h-11 items-center px-1 text-xs font-medium text-gold hover:underline md:min-h-0"
                :aria-label="`Show ${aliases.hidden} more aliases`"
                @click="aliasesOpen = true">
                +{{ aliases.hidden }} more
            </button>
        </p>
        <p
            v-if="entry.kind === 'Character' && entry.claimedByMemberId"
            class="flex">
            <span class="flex items-center gap-1 rounded-md border px-2 py-1 text-sm">
                <UserCheck
                    class="size-4"
                    aria-hidden="true" />
                {{ playedByText }}
            </span>
        </p>
    </header>
</template>

<script setup lang="ts">
    import { History, Image as ImageIcon, Merge, MoreHorizontal, Pencil, UserCheck } from "lucide-vue-next";
    import type { Entry } from "~/utils/api/types";
    import { ENTRY_KIND_ICONS } from "~/utils/entries";
    import { aliasPreview } from "~/utils/entrySections";
    import { GALLERY_VIEWER_SOURCE } from "~/utils/gallery";

    const props = defineProps<{
        campaignId: string;
        entry: Entry;
        viewerMemberId: string;
        canEdit: boolean;
        canChangeAccess: boolean;
        /** The player's name, for a player character's chip (15g, 25c). */
        claimerName?: string;
    }>();
    const emit = defineEmits<{ edit: []; history: []; merge: []; primaryImage: [] }>();

    const src = useImageUrl();
    const imageViewer = useImageViewer();
    // The entry's Gallery section owns that viewer, and the primary image is one of its tiles.
    const openPrimaryImage = () => {
        if (props.entry.primaryImageId) imageViewer.open(props.entry.primaryImageId, GALLERY_VIEWER_SOURCE);
    };

    const aliasesOpen = ref(false);
    const imageFailed = ref(false);
    watch(
        () => props.entry.id,
        () => (aliasesOpen.value = false)
    );
    // A new id, or a new image on the same entry, deserves another try at the bytes.
    watch(
        () => props.entry.primaryImageId,
        () => (imageFailed.value = false)
    );
    const aliases = computed(() => aliasPreview(props.entry.aliases, aliasesOpen.value));

    const playedByText = computed(() =>
        props.entry.claimedByMemberId === props.viewerMemberId
            ? "Played by you"
            : `Played by ${props.claimerName ?? "a player"}`
    );
</script>
