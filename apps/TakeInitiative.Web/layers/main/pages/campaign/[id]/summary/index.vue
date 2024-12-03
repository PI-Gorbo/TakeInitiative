<template>
    <main class="flex flex-col gap-4 px-2 pt-2">
        <header>
            <label class="font-NovaCut text-xl"
                >Welcome to
                {{ campaignStore.state.campaign?.campaignName }}!</label
            >
        </header>

        <IndexCombatBanner />

        <section v-if="description != '' || campaignStore.isDm" class="flex flex-col rounded-md bg-take-purple-dark p-2">
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
        </section>

        <section
            class="flex max-h-[50%] flex-1 flex-col gap-2 overflow-y-auto rounded-md bg-take-purple-dark p-2"
        >
            <label class="block w-full font-NovaCut text-lg"> Players </label>
            <div class="overflow-y-auto">
                <IndexPlayersDisplay
                    :campaignMemberDtos="campaignStore.memberDtos"
                />
            </div>
        </section>

        <section class="w-full flex-1 rounded-md bg-take-purple-dark p-2">
            <label class="font-NovaCut text-lg">Resources</label>
            <IndexResourcesSection />
        </section>

        <section class="w-full flex-1 rounded-md bg-take-purple-dark p-2">
            <label class="font-NovaCut text-lg">Combat History</label>
            <ul class="flex flex-col gap-2 overflow-y-auto">
                <li
                    v-for="finishedCombat in campaignStore.state.combatHistory"
                    :key="finishedCombat.combatName"
                    :class="[
                        'flex items-center justify-between  rounded-md border border-take-purple bg-take-purple p-1 transition-colors',
                    ]"
                >
                    <span class="flex flex-col flex-wrap">
                        <span>{{ finishedCombat.combatName }}</span>
                        <span class="truncate text-wrap text-xs text-take-grey"
                            >Finished on
                            {{
                                new Date(
                                    finishedCombat.finishedOn,
                                ).toLocaleDateString()
                            }}
                            at
                            {{
                                new Date(
                                    finishedCombat.finishedOn,
                                ).toLocaleTimeString(undefined, {
                                    hour: "2-digit",
                                    minute: "2-digit",
                                })
                            }}</span
                        >
                    </span>
                    <div class="flex justify-end">
                        <FormButton
                            icon="eye"
                            label="View"
                            buttonColour="take-purple-light"
                            @clicked="
                                () =>
                                    navigator.toCombatHistory(
                                        campaignStore.state.campaign?.id!,
                                        finishedCombat.combatId,
                                    )
                            "
                        />
                    </div>
                </li>
                <li
                    v-if="campaignStore.state.combatHistory?.length == 0"
                    class="px-2 text-sm text-take-grey"
                >
                    Complete your first combat to see a history here!
                </li>
            </ul>
        </section>

      <section>
        <div class="select-none px-2 text-sm text-take-grey">
          {{ formattedCampaignStartDate }}
        </div>
      </section>
    </main>
</template>
<script setup lang="ts">
import { useForm } from "vee-validate";
import { parseISO, format } from "date-fns";
import { toTypedSchema } from "@vee-validate/zod";
import { z } from "zod";

// Page info
definePageMeta({
    requiresAuth: true,
    layout: "campaign-tabs",
});
const navigator = useNavigator();
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
        "Created " +
        (campaign.value?.createdTimestamp != null
            ? format(parseISO(campaign.value?.createdTimestamp!), "PPPP")
            : ""),
);
const buttonColour = computed(() =>
    descriptionHasChanges.value ? "take-yellow" : "take-navy-medium",
);
</script>
