<template>
    <!-- Stats (15g, glossary: Stats): a Character's optional initiative roll, max HP and
         AC. On a claimed entry everyone who sees it reads them, and the claimer and the
         DMs edit them; on an unclaimed one (an NPC, a monster) only the DMs do, and
         nobody else is sent them. Initiative and HP are dice expressions: the server
         checks them, and its message shows under the field. An entry from a reference
         item offers "Use SRD 5.2 stats" (20d) or "Use 5eTools stats" (21c) to whoever writes
         them, when that item has Stats. On a phone (25e) it is a collapsed row that peeks
         "AC 15 · HP 38 · Init +3", open from the start for the character's player. In the
         desktop's right-hand panel (25f) it is forced open, a headed section with no border. -->
    <WikiEntrySection
        v-if="readable && (entry.stats || writable)"
        title="Stats"
        :peek="statsPeekLabel(entry.stats) || 'No stats yet'"
        :defaultOpen="isPlayer"
        :forceOpen="forceOpen"
        :class="{ 'rounded-md border': !forceOpen }">
        <template #title>
            Stats<span
                v-if="!claimed"
                class="ml-1 font-normal normal-case text-muted-foreground"
                >· 🔒 DMs only</span
            >
        </template>
        <div class="flex flex-wrap items-center gap-x-2">
            <p
                v-if="!editing"
                class="min-w-0 flex-1 text-sm">
                {{ statsLabel(entry.stats) || "No stats yet." }}
            </p>
            <div
                v-else
                class="flex-1" />
            <!-- "Use … stats" (20d, 21c): the source item's Stats into the form, to review and Save. -->
            <Button
                v-if="useSource && entry.source"
                variant="ghost"
                size="sm"
                class="h-11 md:h-7"
                @click="fillFromSource">
                {{ sourceStatsLabel(entry.source) }}
            </Button>
            <Button
                v-if="writable && !editing"
                variant="ghost"
                size="sm"
                class="h-11 md:h-7"
                @click="start">
                {{ entry.stats ? "Edit" : "Add stats" }}
            </Button>
        </div>

        <form
            v-if="editing"
            class="flex flex-col gap-3"
            @submit.prevent="save">
            <div
                class="grid grid-cols-1 gap-3"
                :class="{ 'sm:grid-cols-3': !forceOpen }">
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
    </WikiEntrySection>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { LoaderCircle } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import { apiErrorMessage, apiErrorStatus } from "~/utils/apiErrorParser";
    import type { Entry } from "~/utils/api/types";
    import {
        STATS_EXPRESSION_MAX,
        canReadStats,
        canUseSourceStats,
        canWriteStats,
        claimerOf,
        parseStatsForm,
        statsForm,
        statsLabel,
        type EntryViewer,
        type StatsForm,
        wantsSourceStats,
    } from "~/utils/entries";
    import { putEntryStatsMutation } from "~/utils/queries/entries";
    import { getReferenceItemQuery } from "~/utils/queries/reference";
    import { statsPeekLabel } from "~/utils/entrySections";
    import { sourceStatsLabel } from "~/utils/reference";

    const props = defineProps<{
        campaignId: string;
        entry: Entry;
        viewer: EntryViewer;
        /** The desktop's right-hand panel (25f): always open, and the form one field a row. */
        forceOpen?: boolean;
    }>();

    const claimed = computed(() => !!claimerOf(props.entry));
    // The character's player opens on their stats (25e); everyone else sees the peek.
    const isPlayer = computed(() => claimed.value && claimerOf(props.entry) === props.viewer.memberId);
    const readable = computed(() => canReadStats(props.entry, props.viewer));
    const writable = computed(() => canWriteStats(props.entry, props.viewer));
    watch(writable, (value) => {
        if (!value) editing.value = false;
    });

    const editing = ref(false);
    const form = reactive<StatsForm>({ initiativeRoll: "", maxHp: "", ac: "" });
    const errors = ref<Partial<Record<keyof StatsForm, string>>>({});

    function start() {
        Object.assign(form, statsForm(props.entry.stats));
        errors.value = {};
        editing.value = true;
    }

    // "Use … stats" (20d, 21c): the API derives the item's Stats (`summary.stats`), so the web
    // never does. The item is read once for whoever writes the Stats (it is cached for good),
    // and the button shows only when it has Stats: an SRD monster, or a 5eTools monster whose
    // index row has them. It fills the form; the normal Save writes them.
    const sourceItemQuery = useQuery(
        getReferenceItemQuery(
            () => (wantsSourceStats(props.entry, props.viewer) ? (props.entry.source?.provider ?? "") : ""),
            () => (wantsSourceStats(props.entry, props.viewer) ? (props.entry.source?.externalId ?? "") : "")
        )
    );
    const sourceStats = computed(() => sourceItemQuery.data.value?.summary.stats);
    const useSource = computed(() => canUseSourceStats(props.entry, props.viewer, sourceStats.value));
    function fillFromSource() {
        if (!sourceStats.value) return;
        Object.assign(form, statsForm(sourceStats.value));
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
