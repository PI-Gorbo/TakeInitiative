<template>
    <main class="flex flex-col gap-4 pt-2">
        <header>
            <label class="font-NovaCut text-xl"
                >Welcome to
                {{ campaignStore.state.campaign?.campaignName }}!</label
            >
        </header>
        <IndexCombatBanner />

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
import { useForm } from "vee-validate";
import { parseISO, format } from "date-fns";
import { toTypedSchema } from "@vee-validate/zod";
import { z } from "zod";

// Page info
definePageMeta({
    requiresAuth: true,
    layout: "campaign-tabs",
});

const campaignStore = useCampaignStore();
const campaign = computed(() => campaignStore.state.campaign);
const formState = reactive({
    isSaving: false,
});

// Form Definition for updating campaign description / resources.
// Form Definition
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        z.object({
            description: z.string(),
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
