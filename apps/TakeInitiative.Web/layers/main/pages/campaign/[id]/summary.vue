<template>
    <main class="flex flex-col gap-4 pt-2">
        <header>
            <label class="font-NovaCut text-xl"
                >Welcome to
                {{ campaignStore.state.campaign?.campaignName }}!</label
            >
        </header>
        <header
            v-if="currentCombatInfo"
            :class="[
                'cursor-pointer select-none rounded-lg px-4 py-3 text-center text-xl text-take-navy',
                currentCombatInfo.state == CombatState.Open
                    ? 'bg-take-teal'
                    : 'bg-take-red',
                isMobile ? 'flex-1' : 'w-full',
            ]"
            @click="
                async () => await navigateTo(`/combat/${currentCombatInfo?.id}`)
            "
        >
            <div v-if="currentCombatInfo.state == CombatState.Open">
                {{ openCombatText }}
            </div>
            <div v-else>
                {{ combatStartedText }}
            </div>
        </header>

        <section class="flex flex-col rounded-md bg-take-purple-dark p-2">
            <FormBase
                class="flex flex-1 flex-col"
                :onSubmit="submitDetails"
                v-slot="{ submitting }"
            >
                <div class="flex w-full justify-between font-NovaCut">
                    <label class="text-lg">Campaign Introduction</label>
                    <div v-if="campaignStore.isDm">
                        <FormIconButton
                            icon="floppy-disk"
                            type="submit"
                            :isLoading="!!submitting"
                            :buttonColour="buttonColour"
                            :disabled="!descriptionHasChanges"
                            disabledColour="take-navy"
                        />
                    </div>
                </div>
                <textarea
                    :value="description"
                    class="w-full resize-none rounded-md bg-take-purple-dark px-2 ring-0"
                    :disabled="!campaignStore.isDm"
                    :class="{
                        'my-1 border border-dashed border-take-navy p-1':
                            campaignStore.isDm,
                    }"
                    @input="
                        (e) =>
                            (description = (e.target as HTMLTextAreaElement)
                                .value)
                    "
                    :placeholder="
                        campaignStore.isDm
                            ? 'Write a quick campaign introduction!'
                            : 'Tell your DM to write a quick introduction!'
                    "
                />
            </FormBase>
            <div class="select-none px-2">
                {{ formattedCampaignStartDate }}
            </div>
        </section>

        <section
            class="flex max-h-[50%] flex-1 flex-col gap-4 overflow-y-auto rounded-md bg-take-purple-dark p-2"
        >
            <label class="block w-full font-NovaCut text-lg"> Players </label>
            <div class="overflow-y-auto">
                <IndexPlayersDisplay
                    :campaignMemberDtos="campaignStore.memberDtos"
                />
            </div>
        </section>

        <div class="w-full flex-1 rounded-md bg-take-purple-dark p-2">
            <label class="font-NovaCut text-lg">Resources</label>
            <IndexResourcesSection />
        </div>
    </main>
</template>
<script setup lang="ts">
import { CombatState } from "base/utils/types/models";
import { toTypedSchema } from "@vee-validate/yup";
import { useForm } from "vee-validate";
import { yup } from "base/utils/types/HelperTypes";
import { parseISO, format } from "date-fns";

// Page info
definePageMeta({
    requiresAuth: true,
    layout: "campaign-tabs",
});

const { isMobile } = useDevice();
const userStore = useUserStore();
const campaignStore = useCampaignStore();
const campaign = computed(() => campaignStore.state.campaign);
const formState = reactive({
    isSaving: false,
});

// Form Definition for updating campaign description / resources.
// Form Definition
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            description: yup.string(),
        }),
    ),
    initialValues: {
        description: campaign.value?.campaignDescription ?? "",
    },
});
const [description, descriptionInputProps] = defineField("description");

watch(
    () => campaign.value?.campaignDescription,
    () => {
        description.value = campaign.value?.campaignDescription;
    },
);

const { state: campaignStoreState } = storeToRefs(campaignStore);

const currentCombatInfo = computed(() => {
    return campaignStoreState.value.currentCombatInfo;
});

const descriptionHasChanges = computed(() => {
    return (
        campaignStore.state.campaign?.campaignDescription != description.value
    );
});

async function submitDetails(): Promise<unknown> {
    return await campaignStore.updateCampaignDetails({
        campaignDescription: description.value ?? "",
    });
}

const openCombatText = computed(() => {
    const combatDto = campaignStore.state.currentCombatInfo;
    const combatOpedByName = campaignStore.memberDtos.find(
        (x) => x.userId == combatDto?.dungeonMaster,
    )?.username;

    return `The combat '${combatDto?.combatName}' has been opened. Click to join.`;
});
const combatStartedText = computed(() => {
    const combatDto = campaignStore.state.currentCombatInfo;
    const combatOpedByName = campaignStore.memberDtos.find(
        (x) => x.userId == combatDto?.dungeonMaster,
    )?.username;

    return `The combat '${combatDto?.combatName}' has started! Click to join.`;
});
const formattedCampaignStartDate = computed(
    () =>
        "Created on " +
        (campaign.value?.createdTimestamp != null
            ? format(parseISO(campaign.value?.createdTimestamp!), "PPPP")
            : ""),
);
const buttonColour = computed(() =>
    descriptionHasChanges.value ? "take-yellow" : "take-navy-medium",
);
</script>
