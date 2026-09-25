<template>
    <div
        :id="`session-${session.number}`"
        role="separator"
        :aria-label="label"
        class="flex items-center gap-3 px-4 pb-1 pt-5">
        <span
            class="h-px flex-1 bg-border"
            aria-hidden="true" />
        <form
            v-if="editing"
            class="flex min-w-0 max-w-full items-center gap-1"
            @submit.prevent="save">
            <span class="shrink-0 text-sm font-semibold text-gold">Session {{ session.number }}</span>
            <input
                ref="titleInput"
                v-model="draft"
                type="text"
                maxlength="100"
                enterkeyhint="done"
                placeholder="Title"
                aria-label="Session title"
                class="h-11 min-w-0 flex-1 rounded-md border bg-background px-2 text-base md:h-9 md:text-sm"
                @keydown.esc.prevent="editing = false" />
            <Button
                type="submit"
                size="icon"
                class="size-11 shrink-0 md:size-9"
                aria-label="Save title"
                :disabled="putTitle.isPending.value">
                <Check />
            </Button>
            <Button
                type="button"
                variant="ghost"
                size="icon"
                class="size-11 shrink-0 md:size-9"
                aria-label="Cancel"
                @click="editing = false">
                <X />
            </Button>
        </form>
        <div
            v-else
            class="flex min-w-0 items-center gap-1 text-center text-sm">
            <h2 class="min-w-0 text-muted-foreground">
                <span class="font-semibold text-gold">Session {{ session.number }}</span>
                · <time :datetime="session.startedAt">{{ formatSessionDate(session.startedAt) }}</time>
                <template v-if="session.title">
                    · <span class="text-foreground">{{ session.title }}</span>
                </template>
            </h2>
            <button
                v-if="canEditTitle"
                type="button"
                class="flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground md:size-8"
                :aria-label="session.title ? `Rename Session ${session.number}` : `Add a title to Session ${session.number}`"
                @click="startEditing">
                <Pencil class="size-4" />
            </button>
        </div>
        <span
            class="h-px flex-1 bg-border"
            aria-hidden="true" />
    </div>
</template>

<script setup lang="ts">
    import { Check, Pencil, X } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import type { Session } from "~/utils/api/types";
    import { putSessionTitleMutation } from "~/utils/queries/sessions";
    import { formatSessionDate } from "~/utils/sessionDates";

    const props = defineProps<{
        campaignId: string;
        session: Session;
        /** DMs set and clear session titles. */
        canEditTitle: boolean;
    }>();

    const label = computed(() =>
        [`Session ${props.session.number}`, formatSessionDate(props.session.startedAt), props.session.title]
            .filter(Boolean)
            .join(" · ")
    );

    const editing = ref(false);
    const draft = ref("");
    const titleInput = useTemplateRef<HTMLInputElement>("titleInput");
    const putTitle = putSessionTitleMutation();

    async function startEditing() {
        draft.value = props.session.title ?? "";
        editing.value = true;
        await nextTick();
        titleInput.value?.focus();
    }

    async function save() {
        const title = draft.value.trim();
        if (title === (props.session.title ?? "")) {
            editing.value = false;
            return;
        }
        try {
            await putTitle.mutateAsync({
                campaignId: props.campaignId,
                sessionId: props.session.id,
                title: title || null,
            });
            editing.value = false;
        } catch {
            toast.error("Could not save the title.");
        }
    }
</script>
