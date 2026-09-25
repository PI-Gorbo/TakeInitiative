<template>
    <!-- A stale save (15f, design §4: reload and re-apply). The article is read again
         under the dialog; the viewer's own text of each block they changed is here to
         copy, so nothing they wrote is lost. Copy takes the stored form, so pasted
         mentions keep their links. -->
    <Dialog v-model:open="open">
        <DialogContent class="flex max-h-[90dvh] max-w-lg flex-col">
            <DialogHeader>
                <DialogTitle>Someone else changed this article</DialogTitle>
                <DialogDescription>
                    Reload and re-apply your changes.
                    {{ items.length > 0 ? "Your text is below to copy." : "" }}
                </DialogDescription>
            </DialogHeader>
            <ul
                v-if="items.length > 0"
                class="-mx-1 flex min-h-0 flex-col gap-2 overflow-y-auto px-1">
                <li
                    v-for="item in items"
                    :key="item.key"
                    class="flex flex-col gap-1 rounded-md border p-2">
                    <div class="flex items-center gap-2 text-xs text-muted-foreground">
                        <span>{{ kindLabel(item) }}</span>
                        <div class="flex-1" />
                        <Button
                            variant="ghost"
                            size="sm"
                            class="h-11 gap-1 md:h-7"
                            @click="copy(item.text)">
                            <Copy
                                class="size-3.5"
                                aria-hidden="true" />
                            Copy
                        </Button>
                    </div>
                    <p class="max-h-40 overflow-y-auto whitespace-pre-wrap break-words text-sm">
                        {{ item.display }}
                    </p>
                </li>
            </ul>
            <DialogFooter>
                <Button
                    class="h-11 md:h-9"
                    @click="reload">
                    Reload
                </Button>
            </DialogFooter>
        </DialogContent>
    </Dialog>
</template>

<script setup lang="ts">
    import { Copy } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import type { editedBlocks } from "~/utils/article";

    type Item = ReturnType<typeof editedBlocks>[number];

    defineProps<{ items: Item[] }>();
    const open = defineModel<boolean>("open", { required: true });
    const emit = defineEmits<{ reload: [] }>();

    const kindLabel = (item: Item) =>
        item.kind === "quote" ? "Quote" : item.visibility === "Everyone" ? "Text" : `🔒 ${item.visibility}`;

    async function copy(text: string) {
        try {
            await navigator.clipboard.writeText(text);
            toast.success("Copied.");
        } catch {
            toast.error("Could not copy. Select the text instead.");
        }
    }

    // However it closes, the editor then starts again from the article as it is now.
    const reload = () => (open.value = false);
    watch(open, (now, before) => {
        if (before && !now) emit("reload");
    });
</script>
