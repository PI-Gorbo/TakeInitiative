<template>
    <Card class="flex flex-col gap-3 p-3">
        <!-- "Accepted suggestions" (23e.4): per campaign, the model versions behind mentions the
             viewer linked from a ✨ suggestion in their own notes. Revert unlinks one version's,
             as the viewer's own edits; mentions they typed stay, and created entries are kept. -->
        <div>
            <h3 class="font-medium">✨ Accepted suggestions</h3>
            <p class="text-sm text-muted-foreground">
                Mentions you linked from a suggestion in your own notes, by the
                model version that suggested them.
            </p>
        </div>

        <LoadingFallback
            v-if="loading"
            :isLoading="true"
            :isError="false"
            iconSize="2x" />
        <p
            v-else-if="error"
            class="text-sm text-destructive-tint">
            Could not load your accepted suggestions.
        </p>
        <p
            v-else-if="groups.length === 0"
            class="text-sm text-muted-foreground">
            None yet.
        </p>
        <template v-else>
            <section
                v-for="group in groups"
                :key="group.campaign.id"
                class="flex flex-col gap-1"
                :aria-label="group.campaign.name">
                <h4
                    class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    {{ group.campaign.name }}
                </h4>
                <ul class="flex flex-col">
                    <li
                        v-for="row in group.models"
                        :key="`${row.model}|${row.version}`"
                        class="flex items-center gap-2 border-b py-1 last:border-b-0">
                        <div class="min-w-0 flex-1 text-sm">
                            <p>
                                <span class="font-medium">{{ row.model }}</span>
                                <span class="text-muted-foreground">
                                    ·
                                    {{
                                        mentionsInNotesLabel(
                                            row.mentions,
                                            row.notes
                                        )
                                    }}</span
                                >
                            </p>
                            <p
                                class="truncate text-xs text-muted-foreground"
                                :title="row.version">
                                {{ row.version }}
                            </p>
                        </div>
                        <Button
                            variant="outline"
                            class="h-11 shrink-0 md:h-8"
                            :aria-label="`Revert the mentions ${row.model} suggested in your notes in ${group.campaign.name}`"
                            @click="ask(group.campaign, row)">
                            Revert
                        </Button>
                    </li>
                </ul>
            </section>
        </template>

        <Dialog
            v-if="asked"
            v-model:open="confirmOpen">
            <DialogContent class="max-w-sm">
                <DialogHeader>
                    <DialogTitle>{{
                        result ? "Reverted" : `Revert ${asked.row.model}?`
                    }}</DialogTitle>
                    <DialogDescription v-if="!result">{{
                        revertQuestion(asked.row.mentions)
                    }}</DialogDescription>
                    <DialogDescription v-else>
                        Unlinked
                        {{
                            mentionsInNotesLabel(result.mentions, result.notes)
                        }}.
                    </DialogDescription>
                </DialogHeader>
                <div
                    v-if="result && result.createdEntries.length > 0"
                    class="text-sm">
                    <p>
                        These entries were created from its suggestions and
                        stay:
                    </p>
                    <ul class="mt-1 flex flex-col">
                        <li
                            v-for="entry in result.createdEntries"
                            :key="entry.id">
                            <NuxtLink
                                :to="entryHref(asked.campaign.id, entry.id)"
                                class="flex min-h-11 items-center text-gold hover:underline md:min-h-8"
                                @click="confirmOpen = false">
                                {{ ENTRY_KIND_ICONS[entry.kind] }}
                                {{ entry.name }}
                            </NuxtLink>
                        </li>
                    </ul>
                </div>
                <DialogFooter class="gap-2">
                    <template v-if="!result">
                        <Button
                            variant="ghost"
                            class="h-11 md:h-9"
                            @click="confirmOpen = false">
                            Cancel
                        </Button>
                        <Button
                            variant="destructive"
                            class="h-11 md:h-9"
                            :disabled="revert.isPending.value"
                            @click="confirm">
                            Revert
                        </Button>
                    </template>
                    <Button
                        v-else
                        class="h-11 md:h-9"
                        @click="confirmOpen = false">
                        Done
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    </Card>
</template>

<script setup lang="ts">
    import { useQueries, useQuery } from "@tanstack/vue-query";
    import { toast } from "vue-sonner";
    import type {
        CampaignSummary,
        SuggestionModelUsage,
        SuggestionRevertResult,
    } from "~/utils/api/types";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import { entryHref } from "~/utils/article";
    import { ENTRY_KIND_ICONS } from "~/utils/entries";
    import { getCampaignsQuery } from "~/utils/queries/campaign";
    import {
        getSuggestionModelsQuery,
        revertSuggestionsMutation,
    } from "~/utils/queries/suggestions";
    import { mentionsInNotesLabel, revertQuestion } from "~/utils/suggestions";

    const campaignsQuery = useQuery(getCampaignsQuery());
    const campaigns = computed<CampaignSummary[]>(
        () => campaignsQuery.data.value?.campaigns ?? []
    );
    const modelQueries = useQueries({
        queries: computed(() =>
            campaigns.value.map((c) => getSuggestionModelsQuery(c.id))
        ),
    });

    const loading = computed(
        () =>
            campaignsQuery.isLoading.value ||
            modelQueries.value.some((q) => q.isLoading)
    );
    const error = computed(
        () =>
            campaignsQuery.isError.value ||
            modelQueries.value.some((q) => q.isError)
    );
    // Campaigns with nothing accepted are left out.
    const groups = computed(() =>
        campaigns.value
            .map((campaign, i) => ({
                campaign,
                models: modelQueries.value[i]?.data?.models ?? [],
            }))
            .filter((g) => g.models.length > 0)
    );

    const revert = revertSuggestionsMutation();
    const confirmOpen = ref(false);
    const asked = ref<{
        campaign: CampaignSummary;
        row: SuggestionModelUsage;
    } | null>(null);
    const result = ref<SuggestionRevertResult | null>(null);

    function ask(campaign: CampaignSummary, row: SuggestionModelUsage) {
        asked.value = { campaign, row };
        result.value = null;
        confirmOpen.value = true;
    }

    async function confirm() {
        const target = asked.value;
        if (!target || revert.isPending.value) return;
        try {
            result.value = await revert.mutateAsync({
                campaignId: target.campaign.id,
                model: target.row.model,
                version: target.row.version,
            });
        } catch (err) {
            toast.error(
                apiErrorMessage(err, "Could not revert the suggestions.")
            );
        }
    }
</script>
