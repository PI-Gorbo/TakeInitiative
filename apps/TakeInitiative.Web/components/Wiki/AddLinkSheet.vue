<template>
    <!-- `[+]` on the Links section (27d): a bottom sheet offering the two kinds — **Knowledge
         base** (a search over 26's corpus) and **A link** (a url and a label). Two tabs rather
         than two buttons, because flipping between them must not lose what is typed in the
         other: the API ignores the fields the chosen `kind` does not use.

         It sits `inset` pixels above the bottom so the on-screen keyboard never covers it
         (invariant 11): 13d set `interactive-widget=overlays-content`, so a keyboard overlays
         the page instead of resizing it, and `useKeyboardInset` is how tall it is. -->
    <Sheet v-model:open="open">
        <SheetContent
            side="bottom"
            class="flex max-h-[85dvh] flex-col gap-0 rounded-t-xl p-0 pb-safe"
            :style="inset > 0 ? { bottom: `${inset}px` } : undefined">
            <SheetHeader class="space-y-0.5 border-b px-4 py-3 text-left">
                <SheetTitle class="text-base">Add a link</SheetTitle>
                <SheetDescription class="text-sm">
                    Something from your knowledge base, or any address.
                </SheetDescription>
            </SheetHeader>

            <Tabs
                v-model="tab"
                class="flex min-h-0 flex-col gap-3 p-4">
                <TabsList class="grid h-11 w-full grid-cols-2 md:h-10">
                    <TabsTrigger
                        value="knowledgeBase"
                        class="h-9 md:h-8">
                        Knowledge base
                    </TabsTrigger>
                    <TabsTrigger
                        value="external"
                        class="h-9 md:h-8">
                        A link
                    </TabsTrigger>
                </TabsList>

                <TabsContent
                    value="knowledgeBase"
                    class="mt-0 flex min-h-0 flex-col">
                    <WikiKnowledgeBasePicker
                        :campaignId="campaignId"
                        :links="entry.links"
                        :busy="busy"
                        @pick="addKnowledgeBase" />
                    <p
                        v-if="pickerError"
                        class="pt-2 text-sm text-destructive-tint"
                        role="alert">
                        {{ pickerError }}
                    </p>
                </TabsContent>

                <TabsContent
                    value="external"
                    class="mt-0">
                    <form
                        class="flex flex-col gap-3"
                        @submit.prevent="addExternal">
                        <label class="flex flex-col gap-1.5">
                            <span class="text-sm font-medium">Address</span>
                            <Input
                                v-model="url"
                                type="url"
                                inputmode="url"
                                autocomplete="off"
                                autocapitalize="off"
                                spellcheck="false"
                                enterkeyhint="next"
                                :maxlength="LINK_URL_MAX"
                                placeholder="https://example.com/my-map"
                                :aria-invalid="!!urlError || undefined"
                                class="h-11 text-base md:h-9 md:text-sm" />
                            <span
                                v-if="urlError"
                                class="text-xs text-destructive-tint"
                                role="alert">
                                {{ urlError }}
                            </span>
                        </label>
                        <label class="flex flex-col gap-1.5">
                            <span class="text-sm font-medium">Label</span>
                            <Input
                                v-model="label"
                                autocomplete="off"
                                enterkeyhint="done"
                                :maxlength="LINK_LABEL_MAX"
                                placeholder="the map I drew"
                                :aria-invalid="!!labelError || undefined"
                                class="h-11 text-base md:h-9 md:text-sm" />
                            <span
                                v-if="labelError"
                                class="text-xs text-destructive-tint"
                                role="alert">
                                {{ labelError }}
                            </span>
                        </label>
                        <div class="flex justify-end gap-2">
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
                                :disabled="busy">
                                <LoaderCircle
                                    v-if="busy"
                                    class="animate-spin"
                                    aria-hidden="true" />
                                Add
                            </Button>
                        </div>
                    </form>
                </TabsContent>
            </Tabs>
        </SheetContent>
    </Sheet>
</template>

<script setup lang="ts">
    import { LoaderCircle } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import type { Entry, KnowledgeBaseItem } from "~/utils/api/types";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import {
        LINK_LABEL_MAX,
        LINK_URL_MAX,
        linkLabelError,
        linkUrlError,
        pendingExternalLink,
        pendingKnowledgeBaseLink,
    } from "~/utils/links";
    import { postEntryLinkMutation } from "~/utils/queries/entries";

    const props = defineProps<{ campaignId: string; entry: Entry; viewerMemberId: string }>();
    const open = defineModel<boolean>("open", { required: true });

    const inset = useKeyboardInset();

    const tab = ref<"knowledgeBase" | "external">("knowledgeBase");
    const url = ref("");
    const label = ref("");
    // Shown only after an attempt, so an empty form is not an accusation.
    const urlError = ref<string | null>(null);
    const labelError = ref<string | null>(null);
    const pickerError = ref<string | null>(null);

    // Each opening starts clean, including the tab: the knowledge base is the common case.
    watch(open, (isOpen) => {
        if (!isOpen) return;
        tab.value = "knowledgeBase";
        url.value = "";
        label.value = "";
        urlError.value = null;
        labelError.value = null;
        pickerError.value = null;
    });

    const add = postEntryLinkMutation();
    const busy = computed(() => add.isPending.value);

    /**
     * A field error from the API (`errors.url`, `errors.label`, `errors.itemId`, `errors.links`),
     * or its message. The client checks the same rules before sending, so this is the duplicate
     * 409 and the "pruned since you opened the sheet" 404 — answers only the server has.
     */
    const fieldError = (error: unknown, key: string): string | null => {
        const errors = (error as { response?: { data?: { errors?: Record<string, string[]> } } })?.response?.data?.errors;
        return errors?.[key]?.[0] ?? null;
    };

    async function addKnowledgeBase(item: KnowledgeBaseItem) {
        if (busy.value) return;
        pickerError.value = null;
        try {
            await add.mutateAsync({
                campaignId: props.campaignId,
                entryId: props.entry.id,
                kind: "KnowledgeBase",
                provider: item.provider,
                itemId: item.id,
                optimistic: pendingKnowledgeBaseLink(item, props.viewerMemberId),
            });
            open.value = false;
        } catch (error) {
            pickerError.value =
                fieldError(error, "itemId") ?? fieldError(error, "links") ?? apiErrorMessage(error, "Could not add the link.");
        }
    }

    async function addExternal() {
        if (busy.value) return;
        urlError.value = linkUrlError(url.value);
        labelError.value = linkLabelError(label.value);
        if (urlError.value || labelError.value) return;
        const typed = url.value.trim();
        const named = label.value.trim();
        try {
            await add.mutateAsync({
                campaignId: props.campaignId,
                entryId: props.entry.id,
                kind: "External",
                url: typed,
                label: named,
                optimistic: pendingExternalLink(typed, named, props.viewerMemberId),
            });
            open.value = false;
        } catch (error) {
            urlError.value = fieldError(error, "url");
            labelError.value = fieldError(error, "label");
            if (!urlError.value && !labelError.value) {
                // The 20-link cap is about the entry, not about either field.
                toast.error(fieldError(error, "links") ?? apiErrorMessage(error, "Could not add the link."));
            }
        }
    }
</script>
