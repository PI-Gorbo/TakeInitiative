<template>
    <main class="flex h-full flex-col gap-4 p-2">
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
            <label class="text-lg"> Combat Health Settings </label>
            <li class="py-2">
                <div class="flex flex-row flex-wrap items-center gap-2">
                    <label
                        class="text-md text-gray-300"
                        :class="{
                            'flex-1': !isMobile,
                        }"
                        >How should players see the health of the DM's
                        characters?</label
                    >
                    <Dropdown
                        class="flex-1"
                        :selectedItem="
                            HealthDisplayOptionValueKeyMap[
                                state.combatHealthDisplaySettings
                                    .dmCharacterDisplayMethod ?? 0
                            ] as HealthDisplayOptionKeys
                        "
                        :items="combatHealthDisplayOptionEnumEntries"
                        :displayFunc="healthDisplayOptionsDisplayFunc"
                        :keyFunc="(i) => i"
                        @update:selectedItem="
                            (val) =>
                                (state.combatHealthDisplaySettings.dmCharacterDisplayMethod =
                                    HealthDisplayOptionsEnum[
                                        val as HealthDisplayOptionKeys
                                    ])
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
                            dmCharacterHealthDisplayMethodInfoMessage(
                                state.combatHealthDisplaySettings
                                    .dmCharacterDisplayMethod,
                            )
                        }}
                    </span>
                </footer>
            </li>
            <li class="py-2">
                <div class="flex flex-row flex-wrap items-center gap-2">
                    <label
                        class="text-md text-gray-300"
                        :class="{
                            'flex-1': !isMobile,
                        }"
                        >How should players see the health of Others?</label
                    >
                    <Dropdown
                        class="flex-1"
                        :selectedItem="
                            HealthDisplayOptionValueKeyMap[
                                state.combatHealthDisplaySettings
                                    .otherPlayerCharacterDisplayMethod ?? 0
                            ] as HealthDisplayOptionKeys
                        "
                        :items="combatHealthDisplayOptionEnumEntries"
                        :displayFunc="healthDisplayOptionsDisplayFunc"
                        :keyFunc="(i) => i"
                        @update:selectedItem="
                            (val) =>
                                (state.combatHealthDisplaySettings.otherPlayerCharacterDisplayMethod =
                                    HealthDisplayOptionsEnum[
                                        val as HealthDisplayOptionKeys
                                    ])
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
                            otherPlayerCharacterHealthDisplayMethodInfoMessage(
                                state.combatHealthDisplaySettings
                                    .otherPlayerCharacterDisplayMethod,
                            )
                        }}
                    </span>
                </footer>
            </li>
        </ol>
        <ol class="flex-1">
            <label class="text-lg"> Combat Armour Class Settings </label>
            <li class="py-2">
                <div class="flex flex-row flex-wrap items-center gap-2">
                    <label
                        class="text-md text-gray-300"
                        :class="{
                            'flex-1': !isMobile,
                        }"
                        >How should players see the armour class of the DM's
                        characters?</label
                    >
                    <Dropdown
                        class="flex-1"
                        :selectedItem="
                            ArmourClassDisplayOptionValueKeyMap[
                                state.combatArmourClassDisplaySettings
                                    .dmCharacterDisplayMethod ?? 0
                            ] as ArmourClassDisplayOptionKeys
                        "
                        :items="combatArmourClassDisplayOptionEnumEntries"
                        :displayFunc="armourClassDisplayOptionsDisplayFunc"
                        :keyFunc="(i) => i"
                        @update:selectedItem="
                            (val) =>
                                (state.combatArmourClassDisplaySettings.dmCharacterDisplayMethod =
                                    ArmourClassDisplayOptionsEnum[
                                        val as ArmourClassDisplayOptionKeys
                                    ])
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
                            dmCharacterArmourClassDisplayMethodInfoMessage(
                                state.combatArmourClassDisplaySettings
                                    .dmCharacterDisplayMethod,
                            )
                        }}
                    </span>
                </footer>
            </li>
            <li class="py-2">
                <div class="flex flex-row flex-wrap items-center gap-2">
                    <label
                        class="text-md text-gray-300"
                        :class="{
                            'flex-1': !isMobile,
                        }"
                        >How should players see the armour class of
                        Others?</label
                    >
                    <Dropdown
                        class="flex-1"
                        :selectedItem="
                            ArmourClassDisplayOptionValueKeyMap[
                                state.combatArmourClassDisplaySettings
                                    .otherPlayerCharacterDisplayMethod ?? 0
                            ] as ArmourClassDisplayOptionKeys
                        "
                        :items="combatArmourClassDisplayOptionEnumEntries"
                        :displayFunc="armourClassDisplayOptionsDisplayFunc"
                        :keyFunc="(i) => i"
                        @update:selectedItem="
                            (val) =>
                                (state.combatHealthDisplaySettings.otherPlayerCharacterDisplayMethod =
                                    HealthDisplayOptionsEnum[
                                        val as ArmourClassDisplayOptionKeys
                                    ])
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
                            otherPlayerCharacterArmourClassDisplayMethodInfoMessage(
                                state.combatArmourClassDisplaySettings
                                    .otherPlayerCharacterDisplayMethod,
                            )
                        }}
                    </span>
                </footer>
            </li>
        </ol>
        <li class="flex justify-end">
            <FormButton
                label="Save"
                icon="floppy-disk"
                :loadingDisplay="{
                    showSpinner: true,
                    loadingText: 'Saving...',
                }"
                :disabled="!anyChangesToSave"
                :click="saveChanges"
            />
        </li>
        <footer
            class="mb-8 rounded-md border-2 border-dashed border-take-red p-2"
        >
            <label class="text-lg italic">Danger Zone</label>
            <main class="py-2">
                <FormButton
                    label="Delete Campaign"
                    icon="trash"
                    buttonColour="take-purple-light"
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
import type { UpdateCampaignDetailsRequest } from "base/utils/api/campaign/updateCampaignDetailsRequest";
import {
    HealthDisplayOptionsEnum,
    HealthDisplayOptionValueKeyMap,
    type HealthDisplayOptionValues,
    type HealthDisplayOptionKeys,
    type ArmourClassDisplayOptionValues,
    ArmourClassDisplayOptionValueKeyMap,
    type ArmourClassDisplayOptionKeys,
    ArmourClassDisplayOptionsEnum,
} from "base/utils/types/models";
const { isMobile } = useDevice();
definePageMeta({
    requiresAuth: true,
    layout: "campaign-tabs",
});

const userStore = useUserStore();
const campaignStore = useCampaignStore();
const state = reactive<{
    campaignName: string;
    combatHealthDisplaySettings: {
        dmCharacterDisplayMethod: HealthDisplayOptionValues;
        otherPlayerCharacterDisplayMethod: HealthDisplayOptionValues;
    };
    combatArmourClassDisplaySettings: {
        dmCharacterDisplayMethod: ArmourClassDisplayOptionValues;
        otherPlayerCharacterDisplayMethod: ArmourClassDisplayOptionValues;
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
    combatArmourClassDisplaySettings: {
        dmCharacterDisplayMethod:
            campaignStore.state.campaign?.campaignSettings
                .combatArmourClassDisplaySettings.dmCharacterDisplayMethod!,
        otherPlayerCharacterDisplayMethod:
            campaignStore.state.campaign?.campaignSettings
                .combatArmourClassDisplaySettings
                .otherPlayerCharacterDisplayMethod!,
    },
});

// Saving changes
const campaignNameChanged = computed(
    () => state.campaignName != campaignStore.state.campaign?.campaignName,
);
const dmCharacterHealthDisplayMethodChanged = computed(
    () =>
        state.combatHealthDisplaySettings.dmCharacterDisplayMethod !=
        campaignStore.state.campaign?.campaignSettings
            .combatHealthDisplaySettings.dmCharacterDisplayMethod,
);
const otherCharacterHealthDisplayMethodChanged = computed(
    () =>
        state.combatHealthDisplaySettings.otherPlayerCharacterDisplayMethod !=
        campaignStore.state.campaign?.campaignSettings
            .combatHealthDisplaySettings.otherPlayerCharacterDisplayMethod,
);
const dmCharacterArmourClassDisplayMethodChanged = computed(
    () =>
        state.combatArmourClassDisplaySettings.dmCharacterDisplayMethod !=
        campaignStore.state.campaign?.campaignSettings
            .combatArmourClassDisplaySettings.dmCharacterDisplayMethod,
);
const otherCharacterArmourClassDisplayMethodChanged = computed(
    () =>
        state.combatArmourClassDisplaySettings
            .otherPlayerCharacterDisplayMethod !=
        campaignStore.state.campaign?.campaignSettings
            .combatArmourClassDisplaySettings.otherPlayerCharacterDisplayMethod,
);
const anyChangesToSave = computed(() => {
    return (
        campaignNameChanged.value ||
        dmCharacterHealthDisplayMethodChanged.value ||
        otherCharacterHealthDisplayMethodChanged.value ||
        dmCharacterArmourClassDisplayMethodChanged.value ||
        otherCharacterArmourClassDisplayMethodChanged.value
    );
});
async function saveChanges() {
    const dto: Omit<UpdateCampaignDetailsRequest, "campaignId"> = {};
    if (campaignNameChanged.value) {
        dto.campaignName = state.campaignName;
    }

    if (dmCharacterHealthDisplayMethodChanged.value) {
        if (dto.campaignSettings == null) {
            dto.campaignSettings =
                campaignStore.state.campaign?.campaignSettings!;
        }

        dto.campaignSettings.combatHealthDisplaySettings.dmCharacterDisplayMethod =
            state.combatHealthDisplaySettings.dmCharacterDisplayMethod;
    }

    if (otherCharacterHealthDisplayMethodChanged.value) {
        if (dto.campaignSettings == null) {
            dto.campaignSettings =
                campaignStore.state.campaign?.campaignSettings!;
        }

        dto.campaignSettings.combatHealthDisplaySettings.otherPlayerCharacterDisplayMethod =
            state.combatHealthDisplaySettings.otherPlayerCharacterDisplayMethod;
    }

    if (dmCharacterArmourClassDisplayMethodChanged.value) {
        if (dto.campaignSettings == null) {
            dto.campaignSettings =
                campaignStore.state.campaign?.campaignSettings!;
        }

        dto.campaignSettings.combatArmourClassDisplaySettings.dmCharacterDisplayMethod =
            state.combatArmourClassDisplaySettings.dmCharacterDisplayMethod;
    }

    if (otherCharacterArmourClassDisplayMethodChanged.value) {
        if (dto.campaignSettings == null) {
            dto.campaignSettings =
                campaignStore.state.campaign?.campaignSettings!;
        }

        dto.campaignSettings.combatArmourClassDisplaySettings.otherPlayerCharacterDisplayMethod =
            state.combatArmourClassDisplaySettings.otherPlayerCharacterDisplayMethod;
    }

    await campaignStore.updateCampaignDetails(dto);
}

// Combat Health Display
const combatHealthDisplayOptionEnumEntries = Object.keys(
    HealthDisplayOptionsEnum,
) as HealthDisplayOptionKeys[];

const healthDisplayOptionsDisplayFunc = (opt: HealthDisplayOptionKeys) => {
    switch (opt) {
        case "HealthyBloodied":
            return "Healthy / Bloodied";
        case "RealValue":
            return "Real Value";
        case "Hidden":
            return "Hidden";
    }
};
const dmCharacterHealthDisplayMethodInfoMessage = (
    value: HealthDisplayOptionValues,
) => {
    switch (HealthDisplayOptionValueKeyMap[value]) {
        case "HealthyBloodied":
            return "Displays 'Healthy' for characters above 50% health, 'Blooded' for under 50%, and 'Dead' for 0% for the DM's characters.";
        case "RealValue":
            return "Displays the current health and the max health of the DM's Characters";
        case "Hidden":
            return "The DM's characters health is hidden from others.";
    }
};
const otherPlayerCharacterHealthDisplayMethodInfoMessage = (
    value: HealthDisplayOptionValues,
) => {
    switch (HealthDisplayOptionValueKeyMap[value]) {
        case "HealthyBloodied":
            return "Displays 'Healthy' for characters above 50% health, 'Blooded' for under 50%, and 'Dead' for 0% for other player characters.";
        case "RealValue":
            return "Displays the current health and the max health of other player characters.";
        case "Hidden":
            return "The other player characters health is hidden.";
    }
};

// Combat Armour Class Display
const combatArmourClassDisplayOptionEnumEntries = Object.keys(
    ArmourClassDisplayOptionsEnum,
) as ArmourClassDisplayOptionKeys[];
const armourClassDisplayOptionsDisplayFunc = (
    opt: ArmourClassDisplayOptionKeys,
) => {
    switch (opt) {
        case "RealValue":
            return "Real Value";
        case "Hidden":
            return "Hidden";
    }
};
const dmCharacterArmourClassDisplayMethodInfoMessage = (
    value: ArmourClassDisplayOptionValues,
) => {
    switch (ArmourClassDisplayOptionValueKeyMap[value]) {
        case "RealValue":
            return "Displays the current health and the max health of the DM's Characters";
        case "Hidden":
            return "The DM's characters health is hidden from others.";
    }
};
const otherPlayerCharacterArmourClassDisplayMethodInfoMessage = (
    value: ArmourClassDisplayOptionValues,
) => {
    switch (ArmourClassDisplayOptionValueKeyMap[value]) {
        case "RealValue":
            return "Displays the current health and the max health of other player characters.";
        case "Hidden":
            return "The other player characters health is hidden.";
    }
};
</script>
