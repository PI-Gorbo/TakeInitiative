<template>
    <Dialog v-model:open="open">
        <Button v-bind="props.triggerButtonProps" @click="() => (open = true)">
            <slot name="TriggerButton" />
        </Button>
        <DialogContent>
            <DialogHeader>
                <DialogTitle
                    class="text-center text-2xl font-bold text-destructive">
                    <slot name="Title" />
                </DialogTitle>
                <DialogDescription
                    class="text-center text-sm text-muted-foreground">
                    <slot name="Description" />
                </DialogDescription>
            </DialogHeader>
            <div class="flex gap-2 justify-between">
                <Button
                    v-bind="props.cancelButtonProps"
                    @click="() => (open = false)">
                    <slot name="CancelButton" />
                </Button>

                <Button v-bind="props.confirmButtonProps">
                    <slot name="ConfirmButton" />
                </Button>
            </div>
        </DialogContent>
    </Dialog>
</template>
<script setup lang="ts">
    import type { ButtonProps as ShadButtonProps } from "./ui/button/Button.vue";

    type ButtonProps = ShadButtonProps & HTMLButtonElement;

    const open = ref(false);
    const props = defineProps<{
        triggerButtonProps: ShadButtonProps;
        cancelButtonProps: ShadButtonProps;
        confirmButtonProps: ShadButtonProps;
    }>();
</script>
