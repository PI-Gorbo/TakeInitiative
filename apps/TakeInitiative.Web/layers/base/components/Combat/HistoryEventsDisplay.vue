<template>
    <ul class="flex flex-col gap-2 p-2">
        <li
            v-for="(entry, index) in props.historyResponse.history"
            class="rounded bg-take-purple p-1"
        >
            <header class="flex flex-row justify-between">
                <div class="flex items-center gap-2">
                    <FontAwesomeIcon
                        :class="
                            campaignStore.getMemberDetailsFor(entry.executor)!
                                .isDungeonMaster
                                ? 'text-take-yellow'
                                : 'text-take-teal'
                        "
                        :icon="
                            !campaignStore.getMemberDetailsFor(entry.executor)!
                                .isDungeonMaster
                                ? 'user-large'
                                : 'crown'
                        "
                    />
                    <label
                        >{{ getMemberDto(entry.executor)?.username }}
                        {{ getEntryHeader(entry) }}</label
                    >
                </div>
                <div
                    class="max-w-fit rounded bg-take-creme-medium px-1 align-middle text-black"
                >
                    <span class="border-r border-r-take-navy pr-1">{{
                        index + 1
                    }}</span>
                    <span class="pl-1">
                        {{
                            new Date(entry.timestamp).toLocaleTimeString(
                                "en-US",
                                {
                                    minute: "numeric",
                                    hour: "numeric",
                                },
                            )
                        }}
                    </span>
                </div>
            </header>
            <main>
                <ul>
                    <li v-for="(event, eventIndex) in entry.events">
                        <span v-if="shouldDisplayEventDescription(event)">{{
                            getEventDescription(event)
                        }}</span>
                    </li>
                </ul>
            </main>
        </li>
    </ul>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type { CampaignMemberDto } from "base/utils/api/campaign/getCampaignRequest";
import type { GetCombatHistoryResponse } from "base/utils/api/combat/getCombatHistoryRequest";
import type { HistoryEntry, HistoryEvent } from "base/utils/types/models";

const campaignStore = useCampaignStore();

const props = defineProps<{
    historyResponse: GetCombatHistoryResponse;
}>();
const startTime = computed(
    () => new Date(props.historyResponse.history[0].timestamp),
);
const endTime = computed(() => props.historyResponse.history.at(-1)?.timestamp);

function getMemberDto(id: string) {
    return campaignStore.memberDtos.find((x) => x.userId == id);
}

function getEntryHeader(entry: HistoryEntry): string {
    if (entry.events.length == 1) {
        // If the entry is the combat started entry, display a specialized header.
        const event = entry.events[0];
        switch (event["!"]) {
            case "CombatStarted":
                return "started the Combat";
            case "CombatInitiativeRolled":
                if (event.rolls.length == 0) {
                    return "rolled initiative with no characters.";
                }

                return JSON.stringify(entry.events);
            case "CombatFinished":
                return "finished the Combat";
            default:
                break;
        }
    }

    return ""; // Otherwise display nothing.
}

function shouldDisplayEventDescription(event: HistoryEvent): boolean {
    switch (event["!"]) {
        case "CombatStarted":
            return false;
        case "CombatInitiativeRolled":
            return false;
        case "CombatFinished":
            return false;
        default:
            return true;
    }
}

function getEventDescription(event: HistoryEvent): string {
    switch (event["!"]) {
        case "CombatStarted":
            return "Started the Combat";
        default:
            return JSON.stringify(event);
    }
}
</script>
