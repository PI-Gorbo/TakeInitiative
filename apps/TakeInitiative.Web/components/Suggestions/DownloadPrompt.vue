<template>
    <!-- The one-time download (23d.2): asked before the first download in "Ask", and always on
         mobile data. Nothing downloads until "Download" is tapped. -->
    <Dialog v-model:open="open">
        <DialogContent class="max-h-[90dvh] max-w-md overflow-y-auto">
            <DialogHeader>
                <DialogTitle>✨ Download the suggestion model?</DialogTitle>
                <DialogDescription>
                    The model runs inside your browser, on this device. Your
                    notes are never sent to an outside AI service to be read. It
                    is a one-time {{ size }} download.
                </DialogDescription>
            </DialogHeader>
            <p
                v-if="metered"
                class="text-sm text-muted-foreground">
                This looks like mobile data, so it always asks here first. You
                can change this in
                <NuxtLink
                    to="/app/me"
                    class="font-medium text-gold underline underline-offset-2"
                    @click="open = false">
                    Suggestions on this device</NuxtLink
                >.
            </p>
            <p
                v-else
                class="text-sm text-muted-foreground">
                To choose Off, Ask or Automatic, see
                <NuxtLink
                    to="/app/me"
                    class="font-medium text-gold underline underline-offset-2"
                    @click="open = false">
                    Suggestions on this device</NuxtLink
                >.
            </p>
            <DialogFooter class="gap-2">
                <Button
                    type="button"
                    variant="ghost"
                    class="h-11 md:h-9"
                    @click="open = false">
                    Not now
                </Button>
                <Button
                    type="button"
                    class="h-11 md:h-9"
                    @click="download">
                    Download ({{ size }})
                </Button>
            </DialogFooter>
        </DialogContent>
    </Dialog>
</template>

<script setup lang="ts">
    import { formatMegabytes } from "~/utils/extraction/modelSource";

    const open = defineModel<boolean>("open", { required: true });
    const emit = defineEmits<{ download: [] }>();

    const extractor = useExtractor();
    const size = computed(() => formatMegabytes(extractor.source.value.bytes));
    const metered = ref(false);
    watch(open, (isOpen) => {
        if (isOpen) metered.value = extractor.isMetered();
    });

    function download() {
        open.value = false;
        emit("download");
    }
</script>
