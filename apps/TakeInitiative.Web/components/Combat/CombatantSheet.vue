<template>
    <!-- A combatant's sheet (18d): a bottom sheet on a phone, ending at the keyboard, and
         a dialog from `md`. A DM edits everything; a player their own HP, conditions and,
         while it waits, initiative. Each control saves as it changes, with the whole
         state in one PUT, and the row changes at once (18d.5). There is no Save. -->
    <DialogRoot
        :open="!!combatant && !!fields"
        @update:open="(open) => !open && close()">
        <DialogPortal>
            <DialogOverlay
                class="fixed inset-0 z-50 bg-black/80 data-[state=closed]:animate-out data-[state=open]:animate-in data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
            <DialogContent
                class="fixed inset-x-0 bottom-0 z-50 flex flex-col rounded-t-xl border-t bg-background pb-safe shadow-lg outline-none data-[state=closed]:animate-out data-[state=open]:animate-in data-[state=closed]:slide-out-to-bottom data-[state=open]:slide-in-from-bottom max-md:bottom-[var(--keyboard-inset)] max-md:max-h-[var(--sheet-max)] md:inset-x-auto md:bottom-auto md:left-1/2 md:top-1/2 md:max-h-[85vh] md:w-full md:max-w-lg md:-translate-x-1/2 md:-translate-y-1/2 md:rounded-xl md:border md:pb-0 md:data-[state=closed]:slide-out-to-bottom-0 md:data-[state=open]:slide-in-from-bottom-0"
                :style="{
                    '--keyboard-inset': `${keyboardInset}px`,
                    '--sheet-max': `calc(100dvh - ${keyboardInset}px - 1rem)`,
                }"
                @openAutoFocus="onOpenAutoFocus">
                <template v-if="combatant && fields && edit">
                    <header
                        class="flex shrink-0 items-center gap-2 border-b py-1 pl-4 pr-1">
                        <div class="flex min-w-0 flex-1 flex-col">
                            <DialogTitle
                                class="flex min-w-0 items-center gap-2 truncate text-base font-semibold">
                                <EyeOff
                                    v-if="combatant.hidden && fields.dmFields"
                                    class="size-4 shrink-0 text-muted-foreground"
                                    aria-label="Hidden from players" />
                                <span class="truncate">{{
                                    combatant.name
                                }}</span>
                            </DialogTitle>
                            <DialogDescription
                                class="text-xs text-muted-foreground">
                                {{ subtitle }}
                            </DialogDescription>
                        </div>
                        <DialogClose
                            ref="closeButton"
                            class="flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                            aria-label="Close">
                            <X
                                class="size-5"
                                aria-hidden="true" />
                        </DialogClose>
                    </header>

                    <div
                        class="flex min-h-0 flex-1 flex-col gap-5 overflow-y-auto overscroll-contain p-4">
                        <CombatHpAdjust
                            ref="hpAdjust"
                            :hp="edit.hp"
                            :maxHp="edit.maxHp"
                            @update:hp="(hp) => save((e) => ({ ...e, hp }))"
                            @update:maxHp="
                                (maxHp) => save((e) => withMaxHp(e, maxHp))
                            " />

                        <CombatConditionEditor
                            :modelValue="edit.conditions"
                            @update:modelValue="
                                (conditions) =>
                                    save((e) => ({ ...e, conditions }))
                            " />

                        <div
                            v-if="fields.dmFields || fields.initiative"
                            class="grid grid-cols-2 gap-3">
                            <CombatNumberField
                                v-if="fields.initiative"
                                :modelValue="edit.initiative"
                                label="Initiative"
                                :nullable="fields.dmFields"
                                :min="COMBAT_INITIATIVE_MIN"
                                :max="COMBAT_INITIATIVE_MAX"
                                :placeholder="
                                    combatant.initiativeRoll
                                        ? `🎲 ${combatant.initiativeRoll}`
                                        : 'Waiting'
                                "
                                :hint="
                                    fields.dmFields
                                        ? 'Clear it to wait for the next roll.'
                                        : 'Type what you rolled.'
                                "
                                @update:modelValue="
                                    (initiative) =>
                                        save((e) => ({ ...e, initiative }))
                                " />
                            <CombatNumberField
                                v-if="fields.dmFields"
                                :modelValue="edit.ac"
                                label="AC"
                                nullable
                                :min="0"
                                :max="COMBAT_AC_MAX"
                                placeholder="–"
                                @update:modelValue="
                                    (ac) => save((e) => ({ ...e, ac }))
                                " />
                        </div>

                        <template v-if="fields.dmFields">
                            <label class="flex min-w-0 flex-col gap-1">
                                <span
                                    class="text-xs font-medium text-muted-foreground"
                                    >Name</span
                                >
                                <input
                                    :value="edit.name"
                                    type="text"
                                    enterkeyhint="done"
                                    autocomplete="off"
                                    :maxlength="COMBAT_NAME_MAX"
                                    class="h-11 w-full rounded-md border bg-background px-3 text-base outline-none focus-visible:ring-1 focus-visible:ring-ring md:h-9 md:text-sm"
                                    @change="renameFrom($event)"
                                    @keydown.enter.prevent="
                                        (
                                            $event.target as HTMLInputElement
                                        ).blur()
                                    " />
                            </label>

                            <CombatPlayersSeePicker
                                :modelValue="edit.playersSee"
                                @update:modelValue="
                                    (playersSee) =>
                                        save((e) => ({ ...e, playersSee }))
                                " />

                            <label
                                :for="`${id}-hidden`"
                                class="flex min-h-11 cursor-pointer items-center justify-between gap-3">
                                <span class="flex flex-col">
                                    <span class="text-sm font-medium"
                                        >Hidden from players</span
                                    >
                                    <span class="text-xs text-muted-foreground">
                                        Players see no row, name or count.
                                    </span>
                                </span>
                                <Switch
                                    :id="`${id}-hidden`"
                                    :modelValue="edit.hidden"
                                    @update:modelValue="
                                        (hidden) =>
                                            save((e) => ({
                                                ...e,
                                                hidden: !!hidden,
                                            }))
                                    " />
                            </label>
                        </template>

                        <div
                            v-if="fields.move || fields.remove"
                            class="flex flex-wrap gap-2 border-t pt-4">
                            <template v-if="fields.move">
                                <Button
                                    variant="outline"
                                    class="h-11 gap-2 md:h-9"
                                    :disabled="!moveUp"
                                    @click="
                                        moveUp &&
                                        emit(
                                            'reorder',
                                            combatant.id,
                                            moveUp.afterId
                                        )
                                    ">
                                    <ArrowUp
                                        class="size-4"
                                        aria-hidden="true" />
                                    Move up
                                </Button>
                                <Button
                                    variant="outline"
                                    class="h-11 gap-2 md:h-9"
                                    :disabled="!moveDown"
                                    @click="
                                        moveDown &&
                                        emit(
                                            'reorder',
                                            combatant.id,
                                            moveDown.afterId
                                        )
                                    ">
                                    <ArrowDown
                                        class="size-4"
                                        aria-hidden="true" />
                                    Move down
                                </Button>
                            </template>
                            <Button
                                v-if="fields.remove && !confirmRemove"
                                variant="ghost"
                                class="ml-auto h-11 gap-2 text-destructive-tint hover:text-destructive-tint md:h-9"
                                @click="confirmRemove = true">
                                <Trash2
                                    class="size-4"
                                    aria-hidden="true" />
                                Remove
                            </Button>
                        </div>
                        <div
                            v-if="confirmRemove"
                            role="alertdialog"
                            aria-labelledby="remove-combatant-text"
                            class="flex flex-col gap-2 rounded-md border border-destructive/50 p-3">
                            <p
                                id="remove-combatant-text"
                                class="text-sm">
                                Remove {{ combatant.name }} from the combat?
                                <template v-if="isTurn">
                                    The turn passes to the next combatant.
                                </template>
                            </p>
                            <div class="flex justify-end gap-2">
                                <Button
                                    variant="ghost"
                                    class="h-11 md:h-9"
                                    @click="confirmRemove = false">
                                    Cancel
                                </Button>
                                <Button
                                    variant="destructive"
                                    class="h-11 md:h-9"
                                    :disabled="remove.isPending.value"
                                    @click="removeCombatant">
                                    Remove
                                </Button>
                            </div>
                        </div>
                    </div>
                </template>
            </DialogContent>
        </DialogPortal>
    </DialogRoot>
</template>

<script setup lang="ts">
    import { useQueryClient } from "@tanstack/vue-query";
    import { ArrowDown, ArrowUp, EyeOff, Trash2, X } from "lucide-vue-next";
    import {
        DialogClose,
        DialogContent,
        DialogDescription,
        DialogOverlay,
        DialogPortal,
        DialogRoot,
        DialogTitle,
    } from "reka-ui";
    import { toast } from "vue-sonner";
    import type { Combat } from "~/utils/api/types";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import {
        COMBAT_AC_MAX,
        COMBAT_INITIATIVE_MAX,
        COMBAT_INITIATIVE_MIN,
        COMBAT_NAME_MAX,
        combatantEdit,
        combatantSheetFields,
        moveByOne,
        splitCombatants,
        withCombatantEdit,
        withMaxHp,
        type CombatantEdit,
    } from "~/utils/combat";
    import type { EntryViewer } from "~/utils/entries";
    import {
        applyCombatResponse,
        deleteCombatantMutation,
        getCombatQueryKey,
    } from "~/utils/queries/combats";

    const props = defineProps<{
        campaignId: string;
        combat: Combat;
        viewer: EntryViewer;
    }>();
    /** The combatant whose sheet is open, or null. */
    const combatantId = defineModel<string | null>("combatantId", {
        required: true,
    });
    const emit = defineEmits<{
        reorder: [combatantId: string, afterId: string | null];
    }>();

    const id = useId();
    const keyboardInset = useKeyboardInset();
    const queryClient = useQueryClient();
    const api = useApi();

    const combatant = computed(() =>
        combatantId.value
            ? props.combat.combatants.find((c) => c.id === combatantId.value)
            : undefined
    );
    const fields = computed(() =>
        combatant.value
            ? combatantSheetFields(props.combat, combatant.value, props.viewer)
            : null
    );
    const edit = computed(() =>
        combatant.value ? combatantEdit(combatant.value) : null
    );
    const isTurn = computed(
        () =>
            !!combatant.value &&
            props.combat.turnCombatantId === combatant.value.id
    );

    const subtitle = computed(() => {
        const c = combatant.value;
        if (!c) return "";
        const parts = [
            c.waiting ? "Waiting" : `Initiative ${c.initiative}`,
            ...(isTurn.value ? ["their turn"] : []),
            ...(c.ac != null ? [`AC ${c.ac}`] : []),
        ];
        return parts.join(" · ");
    });

    const ordered = computed(() =>
        splitCombatants(props.combat).ordered.map((c) => c.id)
    );
    const moveUp = computed(() =>
        combatant.value
            ? moveByOne(ordered.value, combatant.value.id, -1)
            : null
    );
    const moveDown = computed(() =>
        combatant.value ? moveByOne(ordered.value, combatant.value.id, 1) : null
    );

    const confirmRemove = ref(false);
    watch(combatantId, () => (confirmRemove.value = false));

    function close() {
        combatantId.value = null;
    }

    // Removed by someone else, or the viewer lost the right to edit it (a role change):
    // the sheet goes.
    watch([combatant, fields], ([c, f]) => {
        if (combatantId.value && (!c || !f)) close();
    });

    // A DM at a keyboard lands in the damage box; on a phone nothing takes focus, so
    // the keyboard stays down until a field is tapped.
    const hpAdjust = ref<{ focus: () => void } | null>(null);
    const closeButton = ref<{ $el: HTMLElement } | null>(null);
    function onOpenAutoFocus(event: Event) {
        event.preventDefault();
        if (window.matchMedia?.("(pointer: fine)").matches)
            nextTick(() => hpAdjust.value?.focus());
        else nextTick(() => closeButton.value?.$el?.focus?.());
    }

    // Saving (18d.5). Each change lands in the cache first, so a second tap builds on
    // the first. The PUTs go one at a time, in order, and only the last response is
    // written back, so an earlier one never undoes a later change on screen.
    let queue: Promise<void> = Promise.resolve();
    let inFlight = 0;
    function save(change: (edit: CombatantEdit) => CombatantEdit) {
        const c = combatant.value;
        if (!c) return;
        const next = change(combatantEdit(c));
        const campaignId = props.campaignId;
        const combatId = props.combat.id;
        const key = getCombatQueryKey(campaignId, combatId);
        queryClient.setQueryData<Combat>(
            key,
            (old) => old && withCombatantEdit(old, c.id, next)
        );
        inFlight++;
        queue = queue.then(async () => {
            try {
                const response = await api.combat.putCombatant({
                    campaignId,
                    combatId,
                    combatantId: c.id,
                    ...next,
                });
                if (inFlight === 1)
                    applyCombatResponse(queryClient, campaignId, response);
            } catch (err) {
                toast.error(apiErrorMessage(err, `Could not save ${c.name}.`));
                void queryClient.invalidateQueries({ queryKey: key });
            } finally {
                inFlight--;
            }
        });
    }

    function renameFrom(event: Event) {
        const input = event.target as HTMLInputElement;
        const name = input.value.trim();
        if (!name) {
            input.value = edit.value?.name ?? "";
            toast.error("A combatant needs a name.");
            return;
        }
        if (name !== edit.value?.name) save((e) => ({ ...e, name }));
    }

    const remove = deleteCombatantMutation();
    async function removeCombatant() {
        const c = combatant.value;
        if (!c) return;
        try {
            await remove.mutateAsync({
                campaignId: props.campaignId,
                combatId: props.combat.id,
                combatantId: c.id,
            });
            close();
        } catch (err) {
            toast.error(apiErrorMessage(err, `Could not remove ${c.name}.`));
        }
    }
</script>
