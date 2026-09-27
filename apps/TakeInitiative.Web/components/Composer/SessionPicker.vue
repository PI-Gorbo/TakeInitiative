<template>
    <!-- "Posting to: Session 13 ▾". Every session, newest first; an older one posts
         "added later". "Start Session N" ends the list for any member. -->
    <DropdownMenu>
        <DropdownMenuTrigger
            class="flex h-11 min-w-0 items-center gap-1 rounded-md px-2 text-sm hover:bg-accent hover:text-accent-foreground md:h-8"
            :aria-label="`Posting to ${target ? `Session ${target.number}` : 'the current session'}. Change session`">
            <span class="hidden text-muted-foreground sm:inline">Posting to:</span>
            <span :class="['truncate font-medium', target && !target.isCurrent && 'text-gold']">
                <span class="sm:hidden">{{ target ? `S${target.number}` : "Current" }}</span>
                <span class="hidden sm:inline">{{ target ? `Session ${target.number}` : "Current session" }}</span>
            </span>
            <ChevronDown
                class="size-4 shrink-0 text-muted-foreground"
                aria-hidden="true" />
        </DropdownMenuTrigger>
        <DropdownMenuContent
            align="start"
            side="top"
            class="max-h-[50dvh] w-64 overflow-y-auto">
            <DropdownMenuLabel>Post to session</DropdownMenuLabel>
            <DropdownMenuRadioGroup
                :modelValue="target?.id"
                @update:modelValue="pick">
                <DropdownMenuRadioItem
                    v-for="option in options"
                    :key="option.session.id"
                    :value="option.session.id"
                    class="min-h-11 md:min-h-8">
                    <span class="flex min-w-0 flex-1 flex-col">
                        <span class="truncate">{{ option.label }}</span>
                        <span
                            v-if="option.addedLater"
                            class="text-xs italic text-muted-foreground">
                            added later
                        </span>
                        <span
                            v-else
                            class="text-xs text-muted-foreground">
                            current
                        </span>
                    </span>
                </DropdownMenuRadioItem>
            </DropdownMenuRadioGroup>
            <DropdownMenuSeparator />
            <DropdownMenuItem
                class="min-h-11 md:min-h-8"
                :disabled="starting || !loaded"
                @select="emit('start')">
                <Plus aria-hidden="true" />
                Start Session {{ nextNumber }}
            </DropdownMenuItem>
        </DropdownMenuContent>
    </DropdownMenu>
</template>

<script setup lang="ts">
    import { ChevronDown, Plus } from "lucide-vue-next";
    import type { AcceptableValue } from "reka-ui";
    import type { Session } from "~/utils/api/types";
    import { nextSessionNumber, sessionOptions, targetSession } from "~/utils/composer";

    const props = defineProps<{
        sessions: Session[];
        /** Loaded yet; "Start Session N" waits for it. */
        loaded: boolean;
        starting: boolean;
    }>();
    /** The picked session id, or null for the current session. */
    const sessionId = defineModel<string | null>({ required: true });
    const emit = defineEmits<{ start: [] }>();

    const options = computed(() => sessionOptions(props.sessions));
    const target = computed(() => targetSession(sessionId.value, props.sessions));
    const nextNumber = computed(() => nextSessionNumber(props.sessions));

    // Picking the current session stores null, so the post follows the current session.
    function pick(value: AcceptableValue) {
        const picked = props.sessions.find((s) => s.id === value);
        sessionId.value = !picked || picked.isCurrent ? null : picked.id;
    }
</script>
