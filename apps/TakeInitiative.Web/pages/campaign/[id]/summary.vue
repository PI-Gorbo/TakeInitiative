<template>
    <main :class="['flex h-full flex-col', !isMobile && 'items-center']">
        <header
            v-if="campaignStore.state.currentCombatInfo"
            :class="[
                'mx-3 my-2 cursor-pointer select-none rounded-lg px-4 py-3 text-center text-xl text-take-navy',
                campaignStore.state.currentCombatInfo.state == CombatState.Open
                    ? 'bg-take-yellow-dark'
                    : 'bg-take-red ',
                isMobile ? 'flex-1' : 'w-full',
            ]"
            @click="
                async () =>
                    await navigateTo(
                        `/combat/${campaignStore.state.currentCombatInfo?.id}`,
                    )
            "
        >
            <div
                v-if="
                    campaignStore.state.currentCombatInfo.state ==
                    CombatState.Open
                "
            >
                {{ openCombatText }}
            </div>
            <div v-else>
                {{ combatStartedText }}
            </div>
        </header>
        <div
            class="grid w-full flex-1 grid-cols-9 gap-4 overflow-y-auto"
            v-if="!isMobile"
        >
            <section class="col-span-3 flex flex-col gap-4 overflow-y-auto">
                <article
                    class="flex flex-1 flex-col rounded-xl border border-take-navy-medium px-2 py-1"
                >
                    <div class="flex-1">
                        <div class="w-full font-NovaCut">Creation Date</div>
                        <div
                            class="px-3 py-2 text-lg text-take-yellow"
                            v-if="campaign?.createdTimestamp"
                        >
                            {{
                                format(
                                    parseISO(campaign?.createdTimestamp ?? ""),
                                    "PPPP",
                                )
                            }}
                        </div>
                    </div>
                    <div class="flex-1">
                        <div class="w-full font-NovaCut">Last Fight Date</div>
                        <div class="px-3 py-2 text-lg text-take-yellow">
                            ...
                        </div>
                    </div>
                </article>
                <section
                    class="flex flex-1 flex-col overflow-y-auto rounded-xl border border-take-navy-medium px-2 py-1"
                >
                    <label class="block w-full font-NovaCut"> Players </label>
                    <div class="overflow-y-auto px-3 py-2">
                        <IndexPlayersDisplay
                            :campaignMemberDtos="campaignStore.memberDtos"
                        />
                    </div>
                </section>
            </section>
            <section class="col-span-6 flex h-full flex-col gap-4">
                <FormBase
                    class="col-span-6 flex flex-1 flex-col rounded-xl border border-take-navy-medium px-2 py-1"
                    :onSubmit="submitDetails"
                    v-slot="{ submitting }"
                >
                    <div class="flex w-full font-NovaCut">
                        <label>Description</label>
                    </div>
                    <div
                        class="flex w-full flex-1 flex-col overflow-y-auto px-3 py-2"
                    >
                        <div
                            class="flex flex-1 flex-row gap-2 rounded-xl bg-take-navy-medium p-1"
                        >
                            <textarea
                                :value="description"
                                class="h-full w-full rounded-xl bg-take-navy-medium p-1 ring-0"
                                :disabled="!campaignStore.isDm"
                                @input="
                                    (e) =>
                                        (description = (
                                            e.target as HTMLTextAreaElement
                                        ).value)
                                "
                            />
                            <div v-if="campaignStore.isDm">
                                <FormButton
                                    type="submit"
                                    class="text-sm disabled:bg-take-navy-medium"
                                    icon="floppy-disk"
                                    :loadingDisplay="{ showSpinner: true }"
                                    :disabled="!descriptionHasChanges"
                                    :isLoading="submitting"
                                />
                            </div>
                        </div>
                    </div>
                </FormBase>
                <FormBase
                    class="col-span-6 flex flex-1 flex-col rounded-xl border border-take-navy-medium px-2 py-1"
                    :onSubmit="submitDetails"
                    v-slot="{ submitting }"
                >
                    <div class="flex w-full font-NovaCut">
                        <label>Resources</label>
                    </div>
                    <div
                        class="flex w-full flex-1 flex-col overflow-y-auto px-3 py-2"
                    >
                        <div
                            class="flex flex-1 flex-row gap-2 rounded-xl bg-take-navy-medium p-1"
                        >
                            <textarea
                                :value="resources"
                                class="h-full w-full rounded-xl bg-take-navy-medium p-1 ring-0"
                                :disabled="!campaignStore.isDm"
                                @input="
                                    (e) =>
                                        (resources = (
                                            e.target as HTMLTextAreaElement
                                        ).value)
                                "
                            />
                            <div v-if="campaignStore.isDm">
                                <FormButton
                                    type="submit"
                                    class="text-sm disabled:bg-take-navy-medium"
                                    icon="floppy-disk"
                                    :loadingDisplay="{ showSpinner: true }"
                                    :disabled="!resourcesHasChanges"
                                    :isLoading="submitting"
                                />
                            </div>
                        </div>
                    </div>
                </FormBase>
            </section>
        </div>
        <div v-else class="flex h-full flex-col gap-2 px-2">
            <article
                class="flex flex-col rounded-xl border border-take-navy-medium px-2 py-1"
            >
                <div class="flex flex-1 flex-row items-center justify-between">
                    <div class="font-NovaCut">Creation Date</div>
                    <div
                        class="px-3 py-2 text-lg text-take-yellow"
                        v-if="campaign?.createdTimestamp"
                    >
                        {{
                            format(
                                parseISO(campaign?.createdTimestamp ?? ""),
                                "PPPP",
                            )
                        }}
                    </div>
                </div>
                <div class="flex flex-1 flex-row items-center justify-between">
                    <div class="font-NovaCut">Last Fight Date</div>
                    <div class="px-3 py-2 text-lg text-take-yellow">...</div>
                </div>
            </article>
            <section
                class="flex flex-col overflow-y-auto rounded-xl border border-take-navy-medium px-2 py-1"
            >
                <label class="block w-full font-NovaCut"> Players </label>
                <div class="overflow-y-auto px-3 py-2">
                    <IndexPlayersDisplay
                        :campaignMemberDtos="campaignStore.memberDtos"
                    />
                </div>
            </section>
            <FormBase
                class="col-span-6 flex flex-1 flex-col rounded-xl border border-take-navy-medium px-2 py-1"
                :onSubmit="submitDetails"
                v-slot="{ submitting }"
            >
                <div
                    class="flex w-full items-center justify-between font-NovaCut"
                >
                    <label>Description</label>
                    <FormButton
                        v-if="campaignStore.isDm"
                        type="submit"
                        class="text-sm disabled:bg-take-navy-medium"
                        icon="floppy-disk"
                        :loadingDisplay="{ showSpinner: true }"
                        :disabled="
                            !descriptionHasChanges && !resourcesHasChanges
                        "
                        :isLoading="submitting"
                    />
                </div>
                <div
                    class="flex w-full flex-1 flex-col overflow-y-auto px-3 py-2"
                >
                    <div
                        class="flex flex-1 flex-row gap-2 rounded-xl bg-take-navy-medium p-1"
                    >
                        <textarea
                            :value="description"
                            class="h-full w-full rounded-xl bg-take-navy-medium p-1 ring-0"
                            :disabled="!campaignStore.isDm"
                            @input="
                                (e) =>
                                    (description = (
                                        e.target as HTMLTextAreaElement
                                    ).value)
                            "
                        />
                        <div></div>
                    </div>
                </div>
                <div class="flex w-full font-NovaCut">
                    <label>Resources</label>
                </div>
                <div
                    class="flex w-full flex-1 flex-col overflow-y-auto px-3 py-2"
                >
                    <div
                        class="flex flex-1 flex-row gap-2 rounded-xl bg-take-navy-medium p-1"
                    >
                        <textarea
                            :value="resources"
                            class="h-full w-full rounded-xl bg-take-navy-medium p-1 ring-0"
                            :disabled="!campaignStore.isDm"
                            @input="
                                (e) =>
                                    (resources = (
                                        e.target as HTMLTextAreaElement
                                    ).value)
                            "
                        />
                    </div>
                </div>
            </FormBase>
        </div>
    </main>
</template>
<script setup lang="ts">
import { CombatState } from "~/utils/types/models";
import { toTypedSchema } from "@vee-validate/yup";
import { useForm } from "vee-validate";
import { yup } from "~/utils/types/HelperTypes";
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
            resources: yup.string(),
        }),
    ),
});
const [description, descriptionInputProps] = defineField("description");
const [resources, resourcesInputProps] = defineField("resources");

onMounted(() => {
    description.value = campaign.value?.campaignDescription ?? "";
    resources.value = campaign.value?.campaignResources ?? "";
});

const descriptionHasChanges = computed(() => {
    return (
        campaignStore.state.campaign?.campaignDescription != description.value
    );
});

const resourcesHasChanges = computed(() => {
    return campaignStore.state.campaign?.campaignResources != resources.value;
});

async function submitDetails(): Promise<unknown> {
    return await campaignStore.updateCampaignDetails({
        campaignDescription: description.value ?? "",
        campaignResources: resources.value ?? "",
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
</script>
