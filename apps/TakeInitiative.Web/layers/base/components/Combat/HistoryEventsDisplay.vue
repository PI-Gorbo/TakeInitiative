<template>
    <ul class="flex flex-col gap-2 overflow-y-auto p-2">
        <li
            v-for="(entry, index) in enrichedEventStream"
            class="rounded bg-take-purple py-1"
        >
            <header class="flex flex-row justify-between px-2">
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
                    class="max-w-fit rounded bg-take-cream-medium px-1 align-middle text-black shadow"
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
                    <li
                        v-for="(event, eventIndex) in entry.events"
                        class="px-2 py-1"
                    >
                        <span
                            v-if="
                                typeof eventBodyComponentMap[event['!']] ==
                                'string'
                            "
                        >
                            {{ eventBodyComponentMap[event["!"]] }}
                        </span>
                        <div
                            v-else-if="
                                eventBodyComponentMap[event['!']] != undefined
                            "
                        >
                            <component
                                :roundNumber="entry.roundNumber"
                                :is="eventBodyComponentMap[event['!']]"
                                :event="event"
                                :nameMap="characterNameMapping"
                            />
                        </div>
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
import { type HistoryEntry, type HistoryEvent } from "base/utils/types/models";
import type { Component } from "vue";
import InitiativeRolledEvent from "./InitiativeRolledEvent.vue";
import TurnStartedEvent from "./TurnStartedEventDisplay.vue";
import TurnEndedEventDisplay from "./TurnEndedEventDisplay.vue";
import RoundEndedEventDisplay from "./RoundEndedEventDisplay.vue";

import CharacterHealthChangedEventDisplay from "./CharacterHealthChangedEventDisplay.vue";
import CharacterConditionAddedEvent from "./CharacterConditionAddedEvent.vue";
import CharacterConditionRemovedEvent from "./CharacterConditionRemovedEvent.vue";

export type HistoryEventComponentProps<TEvent extends HistoryEvent> = {
    roundNumber: number;
    event: TEvent;
    nameMap: typeof characterNameMapping.value;
};

const campaignStore = useCampaignStore();

const props = defineProps<{
    historyResponse: GetCombatHistoryResponse;
}>();

const enrichedEventStream = computed(() => {
    const output = [];
    let roundNumber = 1;
    for (const entry of props.historyResponse.history) {
        const roundEndedEvent = entry.events.find(
            (x) => x["!"] == "RoundEnded",
        );

        output.push({ ...entry, roundNumber });

        if (roundEndedEvent != null) {
            roundNumber++;
        }
    }

    return output;
});

// Computed data from the props.
const startTime = computed(
    () => new Date(enrichedEventStream.value[0].timestamp),
);
const endTime = computed(
    () => new Date(enrichedEventStream.value.at(-1)?.timestamp!),
);
const characterNameMapping = computed(() => {
    const initiativeRolledEvent = enrichedEventStream.value
        .flatMap((x) => x.events)
        .filter((event) => event["!"] == "CombatInitiativeRolled")[0];

    const mapping: Record<string, string> = {};
    for (const ele of initiativeRolledEvent.rolls) {
        mapping[ele.characterId] = ele.characterName;
    }

    return mapping;
});

function getMemberDto(id: string) {
    return campaignStore.memberDtos.find((x) => x.userId == id);
}
function getEntryHeader(entry: HistoryEntry): string {
    if (entry.events.length >= 1) {
        // If the entry is the combat started entry, display a specialized header.
        const event = entry.events[0];

        switch (event["!"]) {
            case "CombatStarted":
                return `started the combat on ${startTime.value.toLocaleString(
                    "en-US",
                    {
                        weekday: "long",
                        day: "numeric",
                        month: "numeric",
                        year: "numeric",
                    },
                )} at ${startTime.value.toLocaleString("en-US", {
                    minute: "numeric",
                    hour: "numeric",
                })}`;
            case "CombatFinished":
                return `finished the combat on ${endTime.value.toLocaleString(
                    "en-US",
                    {
                        weekday: "long",
                        day: "numeric",
                        month: "numeric",
                        year: "numeric",
                    },
                )} at ${endTime.value.toLocaleString("en-US", {
                    minute: "numeric",
                    hour: "numeric",
                })}`;
            default:
                break;
        }
    }

    return ""; // Otherwise display nothing.
}

const eventBodyComponentMap: Record<
    HistoryEvent["!"],
    Component | string | undefined
> = {
    CombatStarted: undefined,
    CombatFinished: undefined,
    CombatInitiativeRolled: InitiativeRolledEvent,
    CharacterHealthChanged: CharacterHealthChangedEventDisplay,
    TurnStarted: TurnStartedEvent,
    TurnEnded: TurnEndedEventDisplay,
    RoundEnded: RoundEndedEventDisplay,
    CharacterRemoved: CharacterConditionRemovedEvent,
    CombatInitiativeModified: InitiativeRolledEvent,
    CharacterConditionAdded: CharacterConditionAddedEvent,
    CharacterConditionRemoved: CharacterConditionRemovedEvent,
};
</script>
