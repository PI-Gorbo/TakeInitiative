<template>
    <!-- New combat (18c.3, DMs): a name, then Create, which opens the new Draft. It goes
         in the current session and moves to the session it starts in. -->
    <Dialog v-model:open="open">
        <DialogContent class="max-h-[90dvh] max-w-md overflow-y-auto">
            <DialogHeader>
                <DialogTitle>New combat</DialogTitle>
                <DialogDescription
                    >A draft only the DMs see, until the first roll starts
                    it.</DialogDescription
                >
            </DialogHeader>
            <form
                class="flex flex-col gap-4"
                @submit.prevent="submit">
                <div class="flex flex-col gap-1.5">
                    <Label :for="`${id}-name`">Name</Label>
                    <Input
                        :id="`${id}-name`"
                        v-model="name"
                        :maxlength="COMBAT_NAME_MAX"
                        autocomplete="off"
                        enterkeyhint="done"
                        class="h-11 text-base md:h-9 md:text-sm"
                        :aria-invalid="!!error || undefined"
                        :aria-describedby="error ? `${id}-error` : undefined" />
                    <p
                        v-if="error"
                        :id="`${id}-error`"
                        class="text-sm text-destructive-tint">
                        {{ error }}
                    </p>
                </div>
                <DialogFooter class="gap-2">
                    <Button
                        type="button"
                        variant="ghost"
                        class="h-11 md:h-9"
                        @click="open = false">
                        Cancel
                    </Button>
                    <Button
                        type="submit"
                        class="h-11 md:h-9"
                        :disabled="!name.trim() || create.isPending.value">
                        Create
                    </Button>
                </DialogFooter>
            </form>
        </DialogContent>
    </Dialog>
</template>

<script setup lang="ts">
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import { COMBAT_NAME_MAX } from "~/utils/combat";
    import { createCombatMutation } from "~/utils/queries/combats";

    const props = defineProps<{ campaignId: string; initialName?: string }>();
    const open = defineModel<boolean>("open", { required: true });

    const id = useId();
    const name = ref("Combat");
    const error = ref<string | null>(null);

    watch(
        open,
        (isOpen) => {
            if (!isOpen) return;
            name.value = props.initialName?.trim() || "Combat";
            error.value = null;
        },
        { immediate: true }
    );
    watch(name, () => (error.value = null));

    const create = createCombatMutation();
    async function submit() {
        const trimmed = name.value.trim();
        if (!trimmed) return;
        try {
            const combat = await create.mutateAsync({
                campaignId: props.campaignId,
                name: trimmed,
            });
            open.value = false;
            await navigateTo(
                `/app/campaigns/${encodeURIComponent(props.campaignId)}/combat/${encodeURIComponent(combat.id)}`
            );
        } catch (err) {
            error.value = apiErrorMessage(err, "Could not create the combat.");
        }
    }
</script>
