<template>
    <!-- A note's menu on desktop (14d). The actions come from `noteActionsFor`, the
         same list the long-press `NoteActionSheet` draws; both emit the choice. -->
    <DropdownMenu
        v-if="actions.length > 0"
        v-model:open="open">
        <DropdownMenuTrigger
            :class="[
                'ml-auto flex size-11 shrink-0 items-center justify-center self-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground -my-3 md:-my-1 md:size-7',
                // On hover or focus with a mouse. On a touch screen a long-press opens the
                // action sheet (14e), so the button is kept only for screen readers.
                '[@media(pointer:coarse)]:sr-only',
                '[@media(pointer:fine)]:opacity-0 [@media(pointer:fine)]:focus-visible:opacity-100 [@media(pointer:fine)]:group-focus-within:opacity-100 [@media(pointer:fine)]:group-hover:opacity-100 data-[state=open]:!opacity-100',
            ]"
            aria-label="Note actions">
            <Ellipsis class="size-4" />
        </DropdownMenuTrigger>
        <DropdownMenuContent
            align="end"
            class="w-52">
            <template
                v-for="action in actions"
                :key="action">
                <DropdownMenuSeparator v-if="action === 'delete'" />
                <DropdownMenuSub v-if="action === 'visibility'">
                    <DropdownMenuSubTrigger class="min-h-11 gap-2 md:min-h-8">
                        <Eye
                            class="size-4"
                            aria-hidden="true" />
                        {{ NOTE_ACTION_LABELS.visibility }}
                    </DropdownMenuSubTrigger>
                    <DropdownMenuSubContent class="w-56">
                        <DropdownMenuRadioGroup
                            :modelValue="note.visibility"
                            @update:modelValue="(v) => emit('select', 'visibility', v as Visibility)">
                            <DropdownMenuRadioItem
                                v-for="option in VISIBILITY_OPTIONS"
                                :key="option.value"
                                :value="option.value"
                                class="min-h-11 md:min-h-8">
                                <span class="flex flex-col">
                                    <span>{{ option.label }}</span>
                                    <span class="text-xs text-muted-foreground">{{ option.hint }}</span>
                                </span>
                            </DropdownMenuRadioItem>
                        </DropdownMenuRadioGroup>
                    </DropdownMenuSubContent>
                </DropdownMenuSub>
                <DropdownMenuItem
                    v-else
                    :class="['min-h-11 md:min-h-8', action === 'delete' && 'text-destructive-tint focus:text-destructive-tint']"
                    @select="emit('select', action)">
                    <component
                        :is="ICONS[action]"
                        aria-hidden="true" />
                    {{ NOTE_ACTION_LABELS[action] }}
                </DropdownMenuItem>
            </template>
        </DropdownMenuContent>
    </DropdownMenu>
</template>

<script setup lang="ts">
    import { BookPlus, Ellipsis, Eye, EyeOff, History, Link, Pencil, Trash2 } from "lucide-vue-next";
    import type { Component } from "vue";
    import type { SessionNote, Visibility } from "~/utils/api/types";
    import { VISIBILITY_OPTIONS } from "~/utils/composer";
    import { NOTE_ACTION_LABELS, type NoteAction } from "~/utils/noteActions";

    defineProps<{
        note: SessionNote;
        actions: NoteAction[];
    }>();
    const emit = defineEmits<{
        select: [action: NoteAction, visibility?: Visibility];
    }>();
    const open = ref(false);

    const ICONS: Record<NoteAction, Component> = {
        promote: BookPlus,
        edit: Pencil,
        visibility: Eye,
        hide: EyeOff,
        unhide: Eye,
        history: History,
        copyLink: Link,
        delete: Trash2,
    };
</script>
