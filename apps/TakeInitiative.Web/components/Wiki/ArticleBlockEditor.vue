<template>
    <!-- One block in the article editor (15f): a text box with `@` (15d's picker, with
         this block's own links), the composer's formatting items, and 🔒 (design §3a).
         On a phone the toolbar and the mention strip are docked above the keyboard
         while this block has focus (invariant 11); from md the toolbar sits under the
         text box and `@` is a popover below the caret. -->
    <div
        :class="[
            'flex flex-col gap-1 rounded-md border p-2',
            kind === 'text' ? 'border-border' : 'border-dashed border-gold/50 bg-gold/5',
        ]"
        :data-block-key="block.key">
        <div class="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
            <span
                v-if="block.quote"
                class="font-medium text-muted-foreground">
                Quote · {{ nameOf(block.quote.authorMemberId) }}, Session
                {{ block.quote.sessionNumber }}
            </span>
            <span
                v-else-if="kind === 'text'"
                class="font-medium text-muted-foreground">
                Text
            </span>
            <WikiSecretBlockPicker
                v-if="kind !== 'text' && canChangeVisibility"
                :modelValue="block.visibility"
                :quote="!!block.quote"
                :ownerName="nameOf(block.ownerMemberId)"
                :ownerIsViewer="block.ownerMemberId === viewer.memberId"
                @update:modelValue="(v) => emit('visibility', v)" />
            <span
                v-else-if="secret"
                class="font-semibold text-gold"
                :title="audience">
                {{ secret }}
            </span>
            <div class="flex-1" />
            <button
                v-for="control in controls"
                :key="control.label"
                type="button"
                :aria-label="control.label"
                :title="control.label"
                :disabled="control.disabled"
                class="flex size-11 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground disabled:opacity-40 md:size-7"
                @mousedown.prevent
                @click="control.run()">
                <component
                    :is="control.icon"
                    class="size-4"
                    aria-hidden="true" />
            </button>
        </div>

        <div class="relative flex flex-col gap-1">
            <textarea
                ref="textarea"
                v-model="block.mention.text"
                :aria-label="label"
                enterkeyhint="enter"
                :aria-controls="mentions.open.value ? `${id}-mentions` : undefined"
                :aria-activedescendant="
                    mentions.open.value ? `${id}-mentions-${mentions.highlighted.value}` : undefined
                "
                :placeholder="kind === 'secret' ? 'Only its readers get this text…' : 'Write…'"
                class="min-h-20 w-full resize-none overflow-hidden rounded-md border bg-background px-3 py-2 text-base leading-snug outline-none focus-visible:ring-1 focus-visible:ring-ring md:text-sm"
                @keydown="onKeydown"
                @input="grow"
                @focus="emit('focus')"
                @blur="emit('blur')" />
            <ComposerMentionStrip
                v-if="desktop"
                :picker="mentions"
                :listId="`${id}-mentions`"
                placement="below" />
            <ComposerMentionLinks
                :state="block.mention"
                :directory="mentions.directory.value" />
        </div>

        <!-- The toolbar: under the text box from md; docked above the keyboard on a
             phone while this block has focus, with the mention strip above it. -->
        <div
            v-if="desktop || active"
            :class="[
                'flex flex-col gap-1',
                !desktop && 'fixed inset-x-0 z-40 border-t bg-background px-2 py-1 px-safe',
            ]"
            :style="desktop ? undefined : dockStyle">
            <ComposerMentionStrip
                v-if="!desktop"
                :picker="mentions"
                :listId="`${id}-mentions`" />
            <ComposerToolbar
                :items="toolbarItems"
                :showPost="false" />
        </div>
    </div>
</template>

<script setup lang="ts">
    import { useMediaQuery } from "@vueuse/core";
    import { ArrowDown, ArrowUp, AtSign, Bold, Italic, List, Lock, Trash2 } from "lucide-vue-next";
    import type { Component } from "vue";
    import type { Visibility } from "~/utils/api/types";
    import { blockKind, secretAudience, secretLabel, type EditorBlock, type TextRange } from "~/utils/article";
    import {
        toggleInline,
        toggleList,
        type ComposerToolbarItem,
        type InlineFormat,
        type TextEdit,
    } from "~/utils/composer";
    import type { EntryViewer } from "~/utils/entries";

    const props = defineProps<{
        campaignId: string;
        /** Reactive, from the editor's list: the text box writes `block.mention`. */
        block: EditorBlock;
        index: number;
        count: number;
        viewer: EntryViewer;
        canChangeVisibility: boolean;
        /** This block has focus (the editor tracks it, so one toolbar is docked at a time). */
        active: boolean;
        nameOf: (memberId: string) => string;
    }>();
    const emit = defineEmits<{
        move: [delta: -1 | 1];
        remove: [];
        secret: [range: TextRange];
        visibility: [visibility: Visibility];
        focus: [];
        blur: [];
        save: [];
    }>();

    const id = useId();
    const kind = computed(() => blockKind(props.block));
    const secret = computed(() => secretLabel(props.block.visibility));
    const audience = computed(() =>
        secretAudience(
            props.block.visibility,
            props.nameOf(props.block.ownerMemberId),
            props.block.ownerMemberId === props.viewer.memberId
        )
    );
    const label = computed(() => {
        const n = `Block ${props.index + 1} of ${props.count}`;
        if (kind.value === "quote") return `${n}, a quote`;
        return kind.value === "secret" ? `${n}, ${secret.value}` : n;
    });

    const desktop = useMediaQuery("(min-width: 768px)");
    const inset = useKeyboardInset();
    const dockStyle = computed(() => ({
        bottom: `${inset.value}px`,
        paddingBottom: inset.value > 0 ? undefined : "calc(0.25rem + env(safe-area-inset-bottom))",
    }));

    const textarea = useTemplateRef<HTMLTextAreaElement>("textarea");
    // eslint-disable-next-line vue/no-setup-props-reactivity-loss -- the block's own reactive text; a new block remounts
    const mentions = useMentionPicker({
        state: props.block.mention,
        textarea,
        campaignId: () => props.campaignId,
    });

    const selection = (): TextRange => {
        const el = textarea.value;
        const end = props.block.mention.text.length;
        return {
            start: el?.selectionStart ?? end,
            end: el?.selectionEnd ?? end,
        };
    };

    function applyEdit(edit: (current: TextEdit) => TextEdit) {
        const el = textarea.value;
        const { start, end } = selection();
        const next = edit({
            text: props.block.mention.text,
            selectionStart: start,
            selectionEnd: end,
        });
        // eslint-disable-next-line vue/no-mutating-props -- the editor's reactive block
        props.block.mention.text = next.text;
        void nextTick(() => {
            grow();
            el?.focus();
            el?.setSelectionRange(next.selectionStart, next.selectionEnd);
        });
    }
    const format = (f: InlineFormat) => applyEdit((e) => toggleInline(e, f));

    const mod = import.meta.client && /Mac|iPhone|iPad/.test(navigator.platform) ? "⌘" : "Ctrl+";
    const toolbarItems = computed<ComposerToolbarItem[]>(() => [
        {
            id: "mention",
            label: "Mention an entry",
            icon: AtSign,
            run: () => mentions.trigger(),
        },
        {
            id: "bold",
            label: "Bold",
            icon: Bold,
            shortcut: `${mod}B`,
            run: () => format("bold"),
        },
        {
            id: "italic",
            label: "Italic",
            icon: Italic,
            shortcut: `${mod}I`,
            run: () => format("italic"),
        },
        {
            id: "list",
            label: "List",
            icon: List,
            run: () => applyEdit(toggleList),
        },
        ...(kind.value === "text"
            ? [
                  {
                      id: "secret",
                      label: "Make secret: the selection, or the whole block",
                      icon: Lock,
                      text: "🔒",
                      separatorBefore: true,
                      run: () => emit("secret", selection()),
                  } satisfies ComposerToolbarItem,
              ]
            : []),
    ]);

    const controls = computed<
        {
            label: string;
            icon: Component;
            disabled?: boolean;
            run: () => void;
        }[]
    >(() => [
        {
            label: "Move up",
            icon: ArrowUp,
            disabled: props.index === 0,
            run: () => emit("move", -1),
        },
        {
            label: "Move down",
            icon: ArrowDown,
            disabled: props.index === props.count - 1,
            run: () => emit("move", 1),
        },
        { label: "Delete block", icon: Trash2, run: () => emit("remove") },
    ]);

    function onKeydown(event: KeyboardEvent) {
        if (mentions.onKeydown(event)) return;
        if ((event.metaKey || event.ctrlKey) && !event.altKey && !event.shiftKey) {
            const key = event.key.toLowerCase();
            if (key === "b" || key === "i") {
                event.preventDefault();
                format(key === "b" ? "bold" : "italic");
            } else if (key === "enter") {
                event.preventDefault();
                emit("save");
            }
        }
    }

    function grow() {
        const el = textarea.value;
        if (!el) return;
        el.style.height = "auto";
        el.style.height = `${el.scrollHeight + (el.offsetHeight - el.clientHeight)}px`;
    }
    onMounted(grow);

    /** Focus the text box, with the caret at the end, and bring the block into view. */
    function focus() {
        const el = textarea.value;
        if (!el) return;
        el.focus({ preventScroll: true });
        el.setSelectionRange(el.value.length, el.value.length);
        el.scrollIntoView({ block: "center", behavior: "smooth" });
    }
    defineExpose({ focus });
</script>
