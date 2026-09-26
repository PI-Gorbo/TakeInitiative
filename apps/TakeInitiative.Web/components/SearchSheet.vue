<template>
    <DialogRoot v-model:open="open">
        <DialogPortal>
            <!-- Full screen on a phone; a centred panel over the page on desktop. -->
            <DialogOverlay
                class="fixed inset-0 z-50 hidden bg-black/80 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 md:block" />
            <DialogContent
                class="fixed inset-0 z-50 flex flex-col bg-background pt-safe px-safe pb-safe data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 md:inset-x-4 md:bottom-auto md:top-[10vh] md:mx-auto md:h-fit md:max-h-[70vh] md:max-w-xl md:rounded-lg md:border md:shadow-lg"
                @openAutoFocus="focusInput">
                <div class="flex items-center gap-2 border-b px-2">
                    <Search class="ml-2 size-5 shrink-0 text-muted-foreground" />
                    <DialogTitle class="sr-only">Search</DialogTitle>
                    <DialogDescription class="sr-only">
                        Search this campaign.
                    </DialogDescription>
                    <input
                        ref="input"
                        v-model="query"
                        type="search"
                        enterkeyhint="search"
                        placeholder="Search"
                        class="h-14 min-w-0 flex-1 bg-transparent text-base outline-none placeholder:text-muted-foreground" />
                    <DialogClose
                        class="flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                        aria-label="Close search">
                        <X class="size-5" />
                    </DialogClose>
                </div>
                <div
                    class="flex flex-1 flex-col items-center justify-center gap-2 p-8 text-center text-muted-foreground">
                    <p>Nothing to search yet.</p>
                    <p class="text-sm">Search arrives in step 17.</p>
                </div>
            </DialogContent>
        </DialogPortal>
    </DialogRoot>
</template>

<script setup lang="ts">
    import { Search, X } from "lucide-vue-next";
    import {
        DialogClose,
        DialogContent,
        DialogDescription,
        DialogOverlay,
        DialogPortal,
        DialogRoot,
        DialogTitle,
    } from "reka-ui";

    const open = defineModel<boolean>("open", { required: true });

    const query = ref("");
    const input = useTemplateRef<HTMLInputElement>("input");

    function focusInput(event: Event) {
        event.preventDefault();
        input.value?.focus();
    }

    // Each open starts with an empty query.
    watch(open, (isOpen) => {
        if (isOpen) query.value = "";
    });
</script>
