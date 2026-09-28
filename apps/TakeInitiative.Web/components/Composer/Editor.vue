<template>
    <!-- The composer's text box (step 17, the rich text box): a TipTap editor in place of 14d's
         textarea, so bold, italic, lists and mentions are drawn as they are written.
         Its text is still the composer's markdown (utils/editorText.ts), so the draft,
         the count and the post are unchanged. The `@` strip or popover is drawn right
         under it; the popover is placed in this box. -->
    <div
        ref="box"
        class="composer-editor relative">
        <EditorContent :editor="editor" />
        <ComposerMentionStrip
            :picker="picker"
            :listId="listId"
            :docked="docked"
            :placement="placement" />
    </div>
</template>

<script lang="ts">
    import type { FocusPosition } from "@tiptap/core";

    /** What the composer (and, next, its edit mode) reach through a template ref. */
    export type ComposerEditorApi = {
        /** Whether the caret is in bold, italic or a list, for the toolbar's `pressed`. */
        active: { bold: boolean; italic: boolean; bulletList: boolean };
        /** The `@` suggestions are showing (the composer hides its "Links" row). */
        mentionsOpen: boolean;
        focus: (position?: FocusPosition) => void;
        toggle: (format: "bold" | "italic" | "bulletList") => void;
        /** The `@` toolbar button: insert `@` and open the picker. */
        triggerMention: () => void;
        /** The caret as an offset into the text, while it is in the first line; else null. */
        caretOffset: () => number | null;
        setCaretOffset: (offset: number) => void;
        hasFocus: () => boolean;
    };
</script>

<script setup lang="ts">
    import { Slice } from "@tiptap/pm/model";
    import { Editor, EditorContent } from "@tiptap/vue-3";
    import { useMediaQuery } from "@vueuse/core";
    import { enterAction } from "~/utils/composer";
    import { caretOffset, composerExtensions, newLine, setCaretOffset } from "~/utils/editorExtensions";
    import { docToText, textToDoc, type MentionAttrs } from "~/utils/editorText";
    import { imageFiles } from "~/utils/images";
    import type { NewEntry } from "~/utils/mentions";

    const props = withDefaults(
        defineProps<{
            /** The text, in the composer's form: markdown, a mention as `@[Name]`. */
            modelValue: string;
            /** What the text's `@[Name]` mentions link to (15d). */
            links: Record<string, string>;
            /** Entries made by Create, sent with the post (15b). */
            newEntries: NewEntry[];
            campaignId: string;
            placeholder?: string;
            label?: string;
            describedBy?: string;
            /** Runs first on every key, and returns true when it handled it: the `/` strip. */
            keydown?: (event: KeyboardEvent) => boolean;
            /** Something else owns the keys (the `/` strip is open). */
            mentionsDisabled?: boolean;
            /** The `@` strip pins itself above the keyboard (an editor that is not pinned). */
            docked?: boolean;
            /** The desktop `@` popover opens above the caret (the composer) or below it. */
            placement?: "above" | "below";
        }>(),
        {
            placeholder: "",
            label: "Session note",
            describedBy: undefined,
            keydown: undefined,
            mentionsDisabled: false,
            docked: false,
            placement: "above",
        }
    );
    const emit = defineEmits<{
        "update:modelValue": [text: string];
        "update:links": [links: Record<string, string>];
        "update:newEntries": [newEntries: NewEntry[]];
        /** Enter on desktop, Mod+Enter anywhere. */
        submit: [];
        /** Escape, with nothing open to close. Editing a note in the composer will cancel. */
        cancel: [];
        focus: [];
        blur: [];
        /** Images pasted into the text. Dropped ones go on to the parent's drop handler. */
        files: [files: File[]];
    }>();

    const id = useId();
    const listId = `${id}-mentions`;
    const touch = useMediaQuery("(pointer: coarse)");
    const box = useTemplateRef<HTMLElement>("box");
    const editor = shallowRef<Editor>();

    // ── Mentions (15d) ───────────────────────────────────────────────────────
    const mentions = useEditorMentionPicker({
        editor,
        box,
        campaignId: () => props.campaignId,
        newEntries: () => props.newEntries,
        addNewEntry: (entry) => emit("update:newEntries", [...props.newEntries, entry]),
        enabled: () => !props.mentionsDisabled,
    });
    const picker = mentions.picker;

    // A new entry (Create) is drawn in gold, as in the "Links" row.
    const mentionClass = (attrs: MentionAttrs) =>
        attrs.isNew ? "rounded bg-gold/10 px-1 font-medium text-gold" : "rounded bg-muted px-1 font-medium";

    // ── Keys ─────────────────────────────────────────────────────────────────
    // One handler, ahead of every plugin's: the `/` strip, then the `@` picker, take
    // the arrows, Tab, Enter and Esc while open. Mod+B, Mod+I and Mod+Shift+8 are
    // StarterKit's own.
    function onKeydown(event: KeyboardEvent): boolean {
        if (props.keydown?.(event)) return true;
        if (picker.onKeydown(event)) return true;
        if (event.isComposing) return false;
        const mod = event.metaKey || event.ctrlKey;
        if (mod && event.key === "Enter") {
            emit("submit");
            return true;
        }
        // Enter posts on desktop; on a touch screen, and with Shift, it is a new line.
        const action = enterAction(event, touch.value);
        if (action === "post") {
            emit("submit");
            return true;
        }
        if (action === "newline" && event.shiftKey) {
            if (editor.value) newLine(editor.value);
            return true;
        }
        if (event.key === "Escape") {
            emit("cancel");
            return false;
        }
        if (mod && !event.altKey && !event.shiftKey && event.key.toLowerCase() === "k") {
            picker.trigger();
            return true;
        }
        return false;
    }

    // ── The editor ───────────────────────────────────────────────────────────
    const attributes = computed(() => ({
        // As 14d's textarea: it grows with its content up to 40% of the screen, then scrolls.
        class: "max-h-[40dvh] min-h-11 w-full overflow-y-auto rounded-md border bg-background px-3 py-2.5 text-base leading-snug outline-none focus-visible:ring-1 focus-visible:ring-ring md:min-h-10 md:py-2 md:text-sm [&_ul]:list-disc [&_ul]:pl-5",
        role: "textbox",
        "aria-multiline": "true",
        "aria-label": props.label,
        enterkeyhint: touch.value ? "enter" : "send",
        ...(props.describedBy ? { "aria-describedby": props.describedBy } : {}),
        ...(picker.open.value
            ? { "aria-controls": listId, "aria-activedescendant": `${listId}-${picker.highlighted.value}` }
            : {}),
    }));

    const editorProps = () => ({
        attributes: attributes.value,
        handleKeyDown: (_view: unknown, event: KeyboardEvent) => onKeydown(event),
        // Pasted images attach (16c); pasted text stays text.
        handlePaste: (_view: unknown, event: ClipboardEvent) => {
            const files = imageFiles(event.clipboardData?.files);
            if (files.length === 0) return false;
            emit("files", files);
            return true;
        },
        // Dropped files are the composer's (its overlay and handler, 16c): claimed
        // here so none land in the text, and left to reach the form.
        handleDrop: (_view: unknown, event: DragEvent) =>
            !!event.dataTransfer && Array.from(event.dataTransfer.types).includes("Files"),
        // Plain text pastes as the composer's markdown: "- a" is a list, "**b**" bold.
        clipboardTextParser: (text: string) => {
            const schema = editor.value!.schema;
            const doc = schema.nodeFromJSON(textToDoc({ text: text.replace(/\r\n?/g, "\n"), links: {} }));
            return Slice.maxOpen(doc.content);
        },
    });

    // The text as last read from, or written to, the parent. A change from outside
    // (a reset after posting, `?about=`, a command, a relinked Create) replaces the
    // document; the editor's own changes do not come back to it.
    let synced = { text: props.modelValue, links: JSON.stringify(props.links) };

    function write() {
        const current = editor.value;
        if (!current) return;
        const out = docToText(current.getJSON());
        const links = JSON.stringify(out.links);
        synced = { text: out.text, links };
        if (out.text !== props.modelValue) emit("update:modelValue", out.text);
        if (links !== JSON.stringify(props.links)) emit("update:links", out.links);
    }

    watch(
        () => [props.modelValue, props.links] as const,
        ([text, links]) => {
            const json = JSON.stringify(links);
            if (text === synced.text && json === synced.links) return;
            synced = { text, links: json };
            editor.value?.commands.setContent(textToDoc({ text, links, newEntries: props.newEntries }), {
                emitUpdate: false,
            });
        },
        { deep: true }
    );

    onMounted(() => {
        editor.value = new Editor({
            content: textToDoc({ text: props.modelValue, links: props.links, newEntries: props.newEntries }),
            extensions: composerExtensions({
                placeholder: () => props.placeholder,
                suggestion: mentions.suggestion,
                mentionClass,
            }),
            editorProps: editorProps(),
            onUpdate: write,
            onFocus: () => {
                mentions.onFocus();
                emit("focus");
            },
            onBlur: () => {
                mentions.onBlur();
                emit("blur");
            },
        });
    });
    onBeforeUnmount(() => editor.value?.destroy());

    // Attributes (the placeholder too) are read when the view updates.
    watch([attributes, () => props.placeholder], () => {
        const current = editor.value;
        if (!current || current.isDestroyed) return;
        current.setOptions({ editorProps: editorProps() });
    });

    // ── The API ──────────────────────────────────────────────────────────────
    const active = computed(() => ({
        bold: editor.value?.isActive("bold") ?? false,
        italic: editor.value?.isActive("italic") ?? false,
        bulletList: editor.value?.isActive("bulletList") ?? false,
    }));

    function toggle(format: "bold" | "italic" | "bulletList") {
        const chain = editor.value?.chain().focus();
        if (!chain) return;
        if (format === "bold") chain.toggleBold().run();
        else if (format === "italic") chain.toggleItalic().run();
        else chain.toggleBulletList().run();
    }

    defineExpose({
        editor,
        active,
        mentionsOpen: picker.open,
        focus: (position?: FocusPosition) => editor.value?.commands.focus(position),
        toggle,
        triggerMention: () => picker.trigger(),
        caretOffset: () => (editor.value ? caretOffset(editor.value) : null),
        setCaretOffset: (offset: number) => editor.value && setCaretOffset(editor.value, offset),
        hasFocus: () => editor.value?.isFocused ?? false,
    });
</script>

<style>
    /* The placeholder (TipTap's Placeholder extension marks the empty first line). */
    .composer-editor .tiptap p.is-editor-empty:first-child::before {
        content: attr(data-placeholder);
        float: left;
        height: 0;
        pointer-events: none;
        color: hsl(var(--muted-foreground));
    }
</style>
