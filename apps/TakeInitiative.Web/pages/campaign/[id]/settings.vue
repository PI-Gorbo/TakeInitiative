<template>
    <main class="flex h-full flex-col gap-4">
        <section class="flex flex-col gap-2">
            <label class="text-lg">Campaign Details</label>
            <FormInput
                class="px-2"
                label="Campaign Name"
                v-model:value="state.campaignName"
                buttonColour="take-navy-medium"
                notEditableColour="take-navy-medium"
            />
        </section>
        <ol class="flex-1">
            <label class="text-lg"> Combat Settings </label>
            <li class="py-2">
                <div class="flex flex-row flex-wrap items-center gap-2">
                    <label class="text-md text-gray-300"
                        >How should players see the health of the DM's
                        characters?</label
                    >
                    <Dropdown
                        class="flex-1"
                        :selectedItem="
                            DisplayOptionValueMap[
                                state.combatHealthDisplaySettings
                                    .dmCharacterDisplayMethod ?? 0
                            ] as DisplayOptions
                        "
                        :items="combatHealthDisplayOptionEnumEntries"
                        :displayFunc="displayOptionsDisplayFunc"
                        :keyFunc="(i) => i"
                        @update:selectedItem="
                            (val) =>
                                (state.combatHealthDisplaySettings.dmCharacterDisplayMethod =
                                    DisplayOptionEnum[val as DisplayOptions])
                        "
                        :hoverOverContent="true"
                        colour="take-navy-dark"
                        hoverColour="take-navy-medium"
                    />
                </div>
                <footer class="flex flex-row items-center gap-2">
                    <FontAwesomeIcon icon="circle-info" />
                    <span class="text-sm text-gray-300">
                        {{
                            dmCharacterDisplayMethodInfoMessage(
                                state.combatHealthDisplaySettings
                                    .dmCharacterDisplayMethod,
                            )
                        }}
                    </span>
                </footer>
            </li>
            <li class="py-2">
                <div class="flex flex-row flex-wrap items-center gap-2">
                    <label class="text-md text-gray-300"
                        >How should players see the health of Others?</label
                    >
                    <Dropdown
                        class="flex-1"
                        :selectedItem="
                            DisplayOptionValueMap[
                                state.combatHealthDisplaySettings
                                    .otherPlayerCharacterDisplayMethod ?? 0
                            ] as DisplayOptions
                        "
                        :items="combatHealthDisplayOptionEnumEntries"
                        :displayFunc="displayOptionsDisplayFunc"
                        :keyFunc="(i) => i"
                        @update:selectedItem="
                            (val) =>
                                (state.combatHealthDisplaySettings.otherPlayerCharacterDisplayMethod =
                                    DisplayOptionEnum[val as DisplayOptions])
                        "
                        :hoverOverContent="true"
                        colour="take-navy-dark"
                        hoverColour="take-navy-medium"
                    />
                </div>
                <footer class="flex flex-row items-center gap-2">
                    <FontAwesomeIcon icon="circle-info" />
                    <span class="text-sm text-gray-300">
                        {{
                            otherPlayerCharacterDisplayMethodInfoMessage(
                                state.combatHealthDisplaySettings
                                    .otherPlayerCharacterDisplayMethod,
                            )
                        }}
                    </span>
                </footer>
            </li>
            <li class="flex justify-end">
                <FormButton
                    label="Save"
                    icon="floppy-disk"
                    :disabled="!anyChangesToSave"
                    :click="saveChanges"
                />
            </li>
        </ol>
        <footer class="mb-8 rounded-md border border-take-red p-2">
            <label class="text-lg">Danger Zone</label>
            <main class="py-2">
                <FormButton
                    label="Delete Campaign"
                    icon="trash"
                    buttonColour="take-navy-light"
                    hoverButtonColour="take-red"
                    size="sm"
                    :click="
                        () =>
                            userStore.deleteCampaign({
                                campaignId: campaignStore.state.campaign?.id!,
                            })
                    "
                />
            </main>
        </footer>
    </main>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import {
    DisplayOptionEnum,
    DisplayOptionValueMap,
    type DisplayOptionValues,
    type DisplayOptions,
} from "~/utils/types/models";

definePageMeta({
    requiresAuth: true,
    layout: "campaign-tabs",
});

const userStore = useUserStore();
const campaignStore = useCampaignStore();
const state = reactive<{
    campaignName: string;
    combatHealthDisplaySettings: {
        dmCharacterDisplayMethod: DisplayOptionValues;
        otherPlayerCharacterDisplayMethod: DisplayOptionValues;
    };
}>({
    campaignName: campaignStore.state.campaign?.campaignName!,
    combatHealthDisplaySettings: {
        dmCharacterDisplayMethod:
            campaignStore.state.campaign?.campaignSettings
                .combatHealthDisplaySettings.dmCharacterDisplayMethod!,
        otherPlayerCharacterDisplayMethod:
            campaignStore.state.campaign?.campaignSettings
                .combatHealthDisplaySettings.otherPlayerCharacterDisplayMethod!,
    },
});

// Saving changes
const anyChangesToSave = computed(() => {
    return (
        state.campaignName != campaignStore.state.campaign?.campaignName ||
        state.combatHealthDisplaySettings.dmCharacterDisplayMethod !=
            campaignStore.state.campaign?.campaignSettings
                .combatHealthDisplaySettings.dmCharacterDisplayMethod ||
        state.combatHealthDisplaySettings.otherPlayerCharacterDisplayMethod !=
            campaignStore.state.campaign?.campaignSettings
                .combatHealthDisplaySettings.otherPlayerCharacterDisplayMethod
    );
});
async function saveChanges() {}

// Combat Health Display
const combatHealthDisplayOptionEnumEntries = Object.keys(
    DisplayOptionEnum,
) as DisplayOptions[];

const displayOptionsDisplayFunc = (opt: DisplayOptions) => {
    switch (opt) {
        case "HealthyBloodied":
            return "Healthy / Bloodied";
        case "RealValue":
            return "Real Value";
        case "Hidden":
            return "Hidden";
    }
};
const dmCharacterDisplayMethodInfoMessage = (value: DisplayOptionValues) => {
    switch (DisplayOptionValueMap[value]) {
        case "HealthyBloodied":
            return "Displays 'Healthy' for characters above 50% health, 'Blooded' for under 50%, and 'Dead' for 0% for the DM's characters.";
        case "RealValue":
            return "Displays the current health and the max health of the DM's Characters";
        case "Hidden":
            return "The DM's characters health is hidden from others.";
    }
};
const otherPlayerCharacterDisplayMethodInfoMessage = (
    value: DisplayOptionValues,
) => {
    switch (DisplayOptionValueMap[value]) {
        case "HealthyBloodied":
            return "Displays 'Healthy' for characters above 50% health, 'Blooded' for under 50%, and 'Dead' for 0% for other player characters.";
        case "RealValue":
            return "Displays the current health and the max health of other player characters.";
        case "Hidden":
            return "The other player characters health is hidden.";
    }
};
</script>
