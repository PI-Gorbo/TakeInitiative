<template>
    <ul>
        <li v-if="userHasNoResources">
            <div class="flex items-center justify-between rounded-md p-2">
                <label class="text-sm text-take-grey">
                    Put your relevant links here.
                    They can be private, between you and the DM, or for everyone!
                </label>
                <FormButton
                    label="Add"
                    icon="plus"
                    buttonColour="take-purple-light"
                    @clicked="showAddResourceModal"
                />
            </div>
        </li>
        <li v-for="memberResources in displayableMemberResources">
            <div class="flex items-center justify-between py-1">
                <label>{{ memberResources.username }}</label>
                <FormButton
                    v-if="
                        memberResources.userId == userStore.state.user?.userId
                    "
                    label="Add"
                    icon="plus"
                    buttonColour="take-purple-light"
                    @clicked="showAddResourceModal"
                />
            </div>
            <ul
                class="flex select-none flex-row gap-2 overflow-x-scroll rounded-md bg-take-purple p-2"
            >
                <li
                    class="flex min-w-min flex-row items-center gap-2 rounded-md bg-take-purple-light p-2"
                    v-for="(resource, index) in memberResources.resources"
                    :key="index"
                >
                    <a
                        class="flex min-w-max cursor-pointer select-none flex-row items-center gap-2"
                        :href="formatLink(resource.link)"
                        target="_blank"
                    >
                        <section
                            class="flex min-w-max cursor-pointer items-center"
                        >
                            <img
                                height="24"
                                width="24"
                                :src="`http://www.google.com/s2/favicons?domain=${resource.link}`"
                            />
                        </section>
                        <label class="cursor-pointer select-none">{{
                            resource.name
                        }}</label>
                    </a>
                    <FormIconButton
                        v-if="
                            memberResources.userId ==
                            userStore.state.user?.userId
                        "
                        icon="pen"
                        buttonColour="take-purple-light"
                        hoverButtonColour="take-yellow"
                        :clicked="
                            async () => showEditResourceModal(index, resource)
                        "
                    />
                </li>
            </ul>
        </li>
        <Modal ref="ModifyResourceModal" title="Add a Link or other Resource">
            <IndexModifyResourceForm
                :addResource="addResource"
                :resource="lastClickedResource.resource"
                :deleteResource="deleteResource"
                :editResource="editResource"
                :key="
                    lastClickedResource.resource != null
                        ? JSON.stringify(lastClickedResource)
                        : -1
                "
            />
        </Modal>
    </ul>
</template>
<script setup lang="ts">
import Modal from "base/components/Modal.vue";
import {
    ResourceVisibilityOptions,
    type CampaignMemberResource,
} from "base/utils/types/models";
const campaignStore = useCampaignStore();
const userStore = useUserStore();
const { memberResources } = storeToRefs(campaignStore);
const userHasNoResources = computed(
    () => (campaignStore.state.userCampaignMember?.resources ?? []).length == 0,
);
const displayableMemberResources = computed(() =>
    memberResources.value
        .map((member) => {
            return {
                ...member,
                resources: (member.resources ?? []).filter((resource) => {
                    switch (resource.visibility) {
                        case ResourceVisibilityOptions.Public:
                            return true;
                        case ResourceVisibilityOptions.DMsOnly:
                            return (
                                campaignStore.isDm ||
                                member.userId == userStore.state.user?.userId
                            );
                        case ResourceVisibilityOptions.Private:
                            return (
                                member.userId == userStore.state.user?.userId
                            );
                        default:
                            return true;
                    }
                }),
            };
        })
        .filter((x) => (x.resources?.length ?? 0) > 0),
);

const ModifyResourceModal = ref<InstanceType<typeof Modal> | null>(null);
const lastClickedResource = reactive<{
    index: number;
    resource: CampaignMemberResource | null;
}>({
    index: -1,
    resource: null,
});
async function showAddResourceModal() {
    lastClickedResource.index = -1;
    lastClickedResource.resource = null;
    ModifyResourceModal.value?.show();
}
async function showEditResourceModal(
    index: number,
    resource: CampaignMemberResource,
) {
    lastClickedResource.index = index;
    lastClickedResource.resource = resource;
    ModifyResourceModal.value?.show();
}
async function addResource(resource: CampaignMemberResource) {
    return await campaignStore
        .setCampaignMemberResources([
            ...(campaignStore.state.userCampaignMember?.resources ?? []),
            resource,
        ])
        .then(() => ModifyResourceModal.value?.hide());
}
async function deleteResource() {
    return await campaignStore
        .setCampaignMemberResources([
            ...(
                campaignStore.state.userCampaignMember?.resources ?? []
            ).toSpliced(lastClickedResource.index, 1),
        ])
        .then(() => ModifyResourceModal.value?.hide());
}
async function editResource(resource: CampaignMemberResource) {
    const array = campaignStore.state.userCampaignMember?.resources ?? [];
    array[lastClickedResource.index] = resource;
    return await campaignStore
        .setCampaignMemberResources(array)
        .then(() => ModifyResourceModal.value?.hide());
}

function formatLink(link: string) {
    return link;
}
</script>
