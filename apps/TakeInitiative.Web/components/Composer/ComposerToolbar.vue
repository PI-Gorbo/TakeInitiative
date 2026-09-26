<template>
    <!-- The composer's toolbar (design §3a). It draws whatever items it is given, so
         step 15 adds @ and step 16 adds 📷 and 🖼 without reshaping it. The article
         editor (15f) draws the same items, plus 🔒, without the ➤ button. -->
    <div
        role="toolbar"
        aria-label="Formatting"
        class="flex items-center gap-0.5">
        <!-- The items scroll sideways when a narrow phone cannot fit them all, so ➤
             always stays in view. On a phone they sit edge to edge: each is 44 px. -->
        <div class="flex min-w-0 flex-1 items-center overflow-x-auto [scrollbar-width:none] md:gap-0.5">
            <template
                v-for="item in items"
                :key="item.id">
                <span
                    v-if="item.separatorBefore"
                    class="mx-1 h-6 w-px shrink-0 bg-border"
                    aria-hidden="true" />
                <button
                    type="button"
                    :title="item.shortcut ? `${item.label} (${item.shortcut})` : item.label"
                    :aria-label="item.label"
                    :aria-pressed="item.pressed === undefined ? undefined : item.pressed"
                    :disabled="item.disabled"
                    :class="[
                        'flex h-11 min-w-11 shrink-0 items-center justify-center gap-1 rounded-md px-2 text-sm transition-colors disabled:opacity-50 md:h-8 md:min-w-8',
                        item.pressed
                            ? 'bg-gold/15 text-gold'
                            : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
                    ]"
                    @mousedown.prevent
                    @click="item.run()">
                    <component
                        :is="item.icon"
                        v-if="item.icon"
                        class="size-4"
                        aria-hidden="true" />
                    <!-- With an icon, the text goes on the narrowest phones so 📷 (16e) fits. -->
                    <span
                        v-if="item.text"
                        aria-hidden="true"
                        :class="item.icon && 'max-[399.98px]:hidden'"
                        >{{ item.text }}</span
                    >
                </button>
            </template>
        </div>

        <slot name="end" />
        <!-- `mousedown.prevent` here and on every item keeps the text box focused, so
             on a phone the keyboard stays up and the pinned composer does not move. -->
        <Button
            v-if="showPost"
            type="submit"
            size="icon"
            class="size-11 shrink-0 md:size-9"
            :aria-label="waiting ? 'Post note once the images are uploaded' : 'Post note'"
            :disabled="!canPost"
            @mousedown.prevent>
            <!-- ➤ pressed while images upload: it posts once they are up (16c). -->
            <LoaderCircle
                v-if="waiting"
                class="animate-spin"
                aria-hidden="true" />
            <SendHorizontal v-else />
        </Button>
    </div>
</template>

<script setup lang="ts">
    import { LoaderCircle, SendHorizontal } from "lucide-vue-next";
    import type { ComposerToolbarItem } from "~/utils/composer";

    withDefaults(
        defineProps<{
            items: ComposerToolbarItem[];
            canPost?: boolean;
            /** The ➤ button; off in the article editor, which saves the whole article. */
            showPost?: boolean;
            /** ➤ was pressed and waits for uploads (16c). */
            waiting?: boolean;
        }>(),
        { canPost: false, showPost: true, waiting: false }
    );
</script>
