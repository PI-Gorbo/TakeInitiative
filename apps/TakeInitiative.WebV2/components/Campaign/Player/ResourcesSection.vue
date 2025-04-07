<template>
    <div class="flex flex-col gap-4">
        <!--Characters-->
        <section>
            <header class="flex gap-2">
                <FontAwesomeIcon :icon="faPerson" />Characters
            </header>
            <div>
                <template v-if="(props.characters ?? []).length === 0">
                    <div
                        v-if="isViewingCurrentUsersData"
                        class="flex flex-1 gap-1 items-center justify-between">
                        <span class="text-gray-500"> None yet... </span>
                        <Button
                            variant="link"
                            @click="() => openDialog('create-character')">
                            <FontAwesomeIcon :icon="faPlusCircle" />
                            New
                        </Button>
                    </div>
                    <div
                        v-else
                        class="flex flex-1 items-center justify-between p">
                        <span class="text-gray-500"> No characters yet. </span>
                    </div>
                </template>
                <template v-else>
                    <ul class="flex flex-col gap-2">
                        <template
                            v-for="(character, index) in props.characters"
                            :key="character.id">
                            <div class="flex items-center">
                                <Button
                                    variant="outline"
                                    class="h-fit flex justify-between w-full items-center disabled:opacity-100"
                                    :class="[
                                        {
                                            'interactable shadow-solid-sm':
                                                isViewingCurrentUsersData,
                                        },
                                    ]"
                                    :disabled="!isViewingCurrentUsersData"
                                    @click="
                                        () => {
                                            dialogState.characterClicked =
                                                character;
                                            openDialog('edit-character');
                                        }
                                    ">
                                    <div
                                        class="flex justify-between flex-wrap gap-2 w-full">
                                        <span>{{ character.name }}</span>
                                        <CampaignCombatCharacterStatsDisplay
                                            :armourClass="character.armourClass"
                                            :health="character.health"
                                            :initiative="
                                                character.initiative.roll
                                            " />
                                    </div>
                                </Button>
                            </div>
                        </template>
                        <div
                            class="flex justify-end"
                            v-if="isViewingCurrentUsersData">
                            <Button
                                variant="link"
                                @click="() => openDialog('create-character')">
                                <FontAwesomeIcon :icon="faPlusCircle" />
                                New
                            </Button>
                        </div>
                    </ul>
                </template>
            </div>
        </section>

        <!--Resources-->
        <section>
            <header class="flex gap-2">
                <FontAwesomeIcon :icon="faNewspaper" />Resources
            </header>
            <!--When there are no resources for this player-->
            <template v-if="(props.resources ?? []).length == 0">
                <div
                    v-if="isViewingCurrentUsersData"
                    class="flex flex-1 gap-1 items-center justify-between">
                    <span class="text-gray-500"> None yet... </span>
                    <Button
                        variant="link"
                        @click="() => openDialog('create-resource')">
                        <FontAwesomeIcon :icon="faPlusCircle" />
                        New
                    </Button>
                </div>
                <div
                    v-else
                    class="flex flex-1 items-center justify-between px-2">
                    <span class="text-gray-500"> No resources yet. </span>
                </div>
            </template>

            <!--Display all resources-->
            <template v-else>
                <ul class="flex flex-col gap-2">
                    <Button
                        v-for="(resource, index) in props.resources"
                        variant="outline"
                        class="interactable shadow-solid-sm disabled:opacity-100"
                        @click="
                            () => {
                                if (isViewingCurrentUsersData) {
                                    dialogState.resourceClicked = {
                                        resource,
                                        index,
                                    };
                                    openDialog('edit-resource');
                                }
                            }
                        ">
                        <ResourceDisplay
                            :resource="resource"
                            :resourceVisibilityOptionNameMap="
                                resourceVisibilityOptionNameMap
                            "
                            :isViewingCurrentUsersData="
                                isViewingCurrentUsersData
                            " />
                    </Button>
                </ul>

                <div class="flex justify-end" v-if="isViewingCurrentUsersData">
                    <Button
                        variant="link"
                        @click="() => openDialog('create-resource')">
                        <FontAwesomeIcon :icon="faPlusCircle" />
                        New
                    </Button>
                </div>
            </template>
        </section>
        <Sheet v-model:open="dialogState.dialogOpen">
            <SheetContent>
                <SheetHeader>
                    <SheetTitle>
                        {{ dialogInfoMap[dialogState.dialogType].title }}
                    </SheetTitle>
                </SheetHeader>
                <Transition name="fade" mode="out-in">
                    <div
                        :key="'create-character'"
                        v-if="dialogState.dialogType == 'create-character'">
                        <CampaignCreatePlayerCharacterForm
                            :onSubmit="createPlayerCharacter" />
                    </div>
                    <div
                        v-else-if="dialogState.dialogType === 'edit-character'">
                        <CampaignEditPlayerCharacterForm
                            ref="editCharacterForm"
                            :character="dialogState.characterClicked!"
                            :onEdit="editCharacter"
                            :onDelete="
                                () =>
                                    deleteCharacter(
                                        dialogState.characterClicked?.id!
                                    )
                            " />
                    </div>
                    <div
                        v-else-if="
                            dialogState.dialogType === 'create-resource'
                        ">
                        <CUDResourceForm
                            type="Add"
                            :addResource="addResource" />
                    </div>
                    <div v-else-if="dialogState.dialogType === 'edit-resource'">
                        <CUDResourceForm
                            type="Edit"
                            :resource="dialogState.resourceClicked?.resource!"
                            :deleteResource="deleteResource"
                            :editResource="editResource" />
                    </div>
                </Transition>
            </SheetContent>
        </Sheet>
    </div>
</template>
<script setup lang="ts">
    import {
        faLink,
        faNewspaper,
        faPen,
        faPerson,
        faPlusCircle,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import type { PlayerCharacterDto } from "~/utils/api/campaign/createPlayerCharacterRequest";

    import {
        resourceVisibilityOptionNameMap,
        ResourceVisibilityOptions,
        type CampaignMemberResource,
        type PlayerCharacter,
    } from "~/utils/types/models";
    import CUDResourceForm from "./CUDResourceForm.vue";
    import { toast } from "vue-sonner";
    import ResourceDisplay from "./ResourceDisplay.vue";
    const campaignStore = useCampaignStore();
    const user = useUserStore();
    const isViewingCurrentUsersData = computed(
        () => user.state.user?.userId == props.userId
    );

    const props = defineProps<{
        userId: string;
        resources: CampaignMemberResource[];
        characters: PlayerCharacter[];
    }>();

    type DialogType =
        | "create-character"
        | "create-resource"
        | "edit-character"
        | "edit-resource";

    const dialogInfoMap: Record<DialogType, { title: string }> = {
        "create-character": {
            title: "Create new character",
        },
        "create-resource": {
            title: "Share a new resource",
        },
        "edit-character": {
            title: "Edit character",
        },
        "edit-resource": {
            title: "Edit resource",
        },
    };

    const dialogState = reactive<{
        dialogOpen: boolean;
        dialogType: DialogType;
        characterClicked: PlayerCharacter | undefined;
        resourceClicked:
            | {
                  resource: CampaignMemberResource;
                  index: number;
              }
            | undefined;
    }>({
        dialogOpen: false,
        dialogType: "create-character",
        characterClicked: undefined,
        resourceClicked: undefined,
    });

    function openDialog(dialogType: DialogType) {
        dialogState.dialogType = dialogType;
        dialogState.dialogOpen = true;
    }

    async function createPlayerCharacter(playerCharacter: PlayerCharacterDto) {
        // Take a snapshot of the player's character list beforehand.
        try {
            const playerCharacterIds =
                campaignStore.state.userCampaignMember?.characters.map(
                    (x) => x.id
                ) ?? [];

            await campaignStore.createPlayerCharacter(playerCharacter);
            toast.success("Created!");

            // Find the new character via id.
            const newIds =
                campaignStore.state.userCampaignMember?.characters.map(
                    (x) => x.id
                ) ?? [];
            const newId = newIds.filter((x) => !playerCharacterIds.includes(x));

            // Swap the dialog to edit mode.
            dialogState.characterClicked =
                campaignStore.state.userCampaignMember?.characters.find(
                    (x) => x.id == newId[0]
                );
            dialogState.dialogType = "edit-character";
        } catch {
            toast.error(
                "Something went wrong while trying to create your new character"
            );
        }
    }

    async function editCharacter(playerCharacter: PlayerCharacterDto) {
        return campaignStore
            .updatePlayerCharacter(
                dialogState.characterClicked?.id!,
                playerCharacter
            )
            .then(() => (dialogState.dialogOpen = false))
            .then(() => toast.success("Saved!"))
            .then(() =>
                toast.error(
                    "Something went wrong while trying to edit your character"
                )
            );
    }

    async function deleteCharacter(characterId: string) {
        return campaignStore
            .deletePlayerCharacter(characterId)
            .then(() => (dialogState.dialogOpen = false))
            .then(() => toast.success("Deleted!"))
            .catch(() =>
                toast.error(
                    "Something went wrong while trying to delete your character."
                )
            );
    }

    async function addResource(resource: CampaignMemberResource) {
        return await campaignStore
            .setCampaignMemberResources([
                ...(campaignStore.state.userCampaignMember?.resources ?? []),
                resource,
            ])
            .then(() => (dialogState.dialogOpen = false))
            .then(() => toast.success("Added Resource"))
            .catch(() =>
                toast.error(
                    "Something went wrong while trying to add resource."
                )
            );
    }

    async function deleteResource() {
        return await campaignStore
            .setCampaignMemberResources([
                ...(
                    campaignStore.state.userCampaignMember?.resources ?? []
                ).toSpliced(dialogState.resourceClicked?.index!, 1),
            ])
            .then(() => (dialogState.dialogOpen = false))
            .then(() => toast.success("Deleted Resource"))
            .catch(() =>
                toast.error(
                    "Something went wrong while trying to delete resource."
                )
            );
    }

    async function editResource(resource: CampaignMemberResource) {
        const array = campaignStore.state.userCampaignMember?.resources ?? [];
        array[dialogState.resourceClicked?.index!] = resource;
        return await campaignStore
            .setCampaignMemberResources(array)
            .then(() => (dialogState.dialogOpen = false))
            .then(() => toast.success("Updated Resource"))
            .catch(() =>
                toast.error(
                    "Something went wrong while trying to update resource."
                )
            );
    }
</script>
