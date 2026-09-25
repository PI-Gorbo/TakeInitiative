<template>
    <!-- Stats (15g, glossary: Stats): a Character's optional initiative roll, max HP and
         AC. On a claimed entry everyone who sees it reads them, and the claimer and the
         DMs edit them; on an unclaimed one (an NPC, a monster) only the DMs do, and
         nobody else is sent them. Initiative and HP are dice expressions: the server
         checks them, and its message shows under the field. -->
    <section
        v-if="readable && (entry.stats || writable)"
        aria-label="Stats"
        class="flex flex-col gap-2 rounded-md border px-3 py-2">
        <div class="flex items-center gap-2">
            <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Stats<span
                    v-if="!claimed"
                    class="ml-1 normal-case"
                    >· 🔒 DMs only</span
                >
            </h3>
            <div class="flex-1" />
            <Button
                v-if="writable && !editing"
                variant="ghost"
                size="sm"
                class="h-11 md:h-7"
                @click="start">
                {{ entry.stats ? "Edit" : "Add stats" }}
            </Button>
        </div>

        <p
            v-if="!editing"
            class="text-sm">
            {{ statsLabel(entry.stats) || "No stats yet." }}
        </p>
        <form
            v-else
            class="flex flex-col gap-3"
            @submit.prevent="save">
            <div class="grid grid-cols-1 gap-3 sm:grid-cols-3">
                <label class="flex flex-col gap-1 text-sm">
                    <span class="font-medium">Initiative roll</span>
                    <input
                        v-model="form.initiativeRoll"
                        placeholder="1d20+2"
                        autocomplete="off"
                        :maxlength="STATS_EXPRESSION_MAX"
                        :aria-invalid="!!errors.initiativeRoll"
                        class="h-11 rounded-md border bg-background px-3 text-base outline-none focus-visible:ring-1 focus-visible:ring-ring md:h-9 md:text-sm" />
                    <span
                        v-if="errors.initiativeRoll"
                        class="text-xs text-destructive-tint"
                        >{{ errors.initiativeRoll }}</span
                    >
                </label>
                <label class="flex flex-col gap-1 text-sm">
                    <span class="font-medium">Max HP</span>
                    <input
                        v-model="form.maxHp"
                        placeholder="2d8+2 or 11"
                        autocomplete="off"
                        :maxlength="STATS_EXPRESSION_MAX"
                        :aria-invalid="!!errors.maxHp"
                        class="h-11 rounded-md border bg-background px-3 text-base outline-none focus-visible:ring-1 focus-visible:ring-ring md:h-9 md:text-sm" />
                    <span
                        v-if="errors.maxHp"
                        class="text-xs text-destructive-tint"
                        >{{ errors.maxHp }}</span
                    >
                </label>
                <label class="flex flex-col gap-1 text-sm">
                    <span class="font-medium">AC</span>
                    <input
                        v-model="form.ac"
                        inputmode="numeric"
                        placeholder="15"
                        autocomplete="off"
                        :aria-invalid="!!errors.ac"
                        class="h-11 rounded-md border bg-background px-3 text-base outline-none focus-visible:ring-1 focus-visible:ring-ring md:h-9 md:text-sm" />
                    <span
                        v-if="errors.ac"
                        class="text-xs text-destructive-tint"
                        >{{ errors.ac }}</span
                    >
                </label>
            </div>
            <div class="flex justify-end gap-2">
                <Button
                    type="button"
                    variant="ghost"
                    class="h-11 md:h-8"
                    @click="editing = false">
                    Cancel
                </Button>
                <Button
                    type="submit"
                    class="h-11 md:h-8"
                    :disabled="busy">
                    <LoaderCircle
                        v-if="busy"
                        class="animate-spin"
                        aria-hidden="true" />
                    Save
                </Button>
            </div>
        </form>
    </section>
</template>

<script setup lang="ts">
    import { LoaderCircle } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import { apiErrorMessage, apiErrorStatus } from "~/utils/apiErrorParser";
    import type { Entry } from "~/utils/api/types";
    import {
        STATS_EXPRESSION_MAX,
        canReadStats,
        canWriteStats,
        claimerOf,
        parseStatsForm,
        statsLabel,
        type EntryViewer,
        type StatsForm,
    } from "~/utils/entries";
    import { putEntryStatsMutation } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        entry: Entry;
        viewer: EntryViewer;
    }>();

    const claimed = computed(() => !!claimerOf(props.entry));
    const readable = computed(() => canReadStats(props.entry, props.viewer));
    const writable = computed(() => canWriteStats(props.entry, props.viewer));
    watch(writable, (value) => {
        if (!value) editing.value = false;
    });

    const editing = ref(false);
    const form = reactive<StatsForm>({ initiativeRoll: "", maxHp: "", ac: "" });
    const errors = ref<Partial<Record<keyof StatsForm, string>>>({});

    function start() {
        form.initiativeRoll = props.entry.stats?.initiativeRoll ?? "";
        form.maxHp = props.entry.stats?.maxHp ?? "";
        form.ac = props.entry.stats?.ac != null ? String(props.entry.stats.ac) : "";
        errors.value = {};
        editing.value = true;
    }

    const mutation = putEntryStatsMutation();
    const busy = computed(() => mutation.isPending.value);

    async function save() {
        const parsed = parseStatsForm(form);
        errors.value = parsed.errors;
        if (!parsed.body) return;
        try {
            await mutation.mutateAsync({ campaignId: props.campaignId, entryId: props.entry.id, ...parsed.body });
            editing.value = false;
        } catch (error) {
            const fields = (error as { response?: { data?: { errors?: Record<string, string[]> } } })?.response?.data
                ?.errors;
            if (apiErrorStatus(error) === 400 && fields) {
                errors.value = {
                    initiativeRoll: fields.initiativeRoll?.[0],
                    maxHp: fields.maxHp?.[0],
                    ac: fields.ac?.[0],
                };
                if (Object.values(errors.value).some(Boolean)) return;
            }
            toast.error(apiErrorMessage(error, "Could not save the stats."));
        }
    }
</script>
