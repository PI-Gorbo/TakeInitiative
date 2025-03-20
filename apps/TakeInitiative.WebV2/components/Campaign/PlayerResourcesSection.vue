<template>
    <div class="flex flex-col gap-4">
        <section>
            <header>Characters</header>
            <div class="px-2">
                <template v-if="(props.characters ?? []).length === 0">
                    <div
                        v-if="isViewingCurrentUsersCharacters"
                        class="flex flex-1 gap-1 items-center justify-between">
                        <span class="text-gray-500">
                            You've not got any characters yet.
                        </span>
                        <Button
                            variant="outline"
                            class="interactable shadow-solid-sm"
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
                        <Button
                            v-for="(character, index) in props.characters"
                            :key="character.id"
                            variant="outline"
                            :class="{
                                'interactable shadow-solid-sm':
                                    isViewingCurrentUsersCharacters,
                            }"
                            :disabled="!isViewingCurrentUsersCharacters"
                            @click="
                                () => {
                                    dialogState.characterClicked = character;
                                    openDialog('edit-character');
                                }
                            ">
                            <FontAwesomeIcon :icon="faPerson" />
                            {{ character.name }}
                        </Button>
                        <div
                            class="flex justify-end"
                            v-if="isViewingCurrentUsersCharacters">
                            <Button
                                variant="outline"
                                class="interactable shadow-solid-sm"
                                @click="() => openDialog('create-character')">
                                <FontAwesomeIcon :icon="faPlusCircle" />
                                New
                            </Button>
                        </div>
                    </ul>
                </template>
            </div>
        </section>
        <section>
            <header>Resources</header>
            <template v-if="(props.resources ?? []).length == 0">
                <div
                    v-if="isViewingCurrentUsersCharacters"
                    class="flex flex-1 gap-1 items-center justify-between px-2">
                    <span class="text-gray-500"
                        >You've not got any resources yet.</span
                    >
                    <Button
                        variant="outline"
                        class="interactable shadow-solid-sm"
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
        </section>
        <Dialog
            :open="dialogState.dialogOpen"
            @update:open="
                (dialogValue) => {
                    if (dialogValue == false) {
                        trySubmitThenClose();
                    }
                }
            ">
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>
                        {{ dialogInfoMap[dialogState.dialogType].title }}
                    </DialogTitle>
                </DialogHeader>
                <Transition name="fade" mode="out-in">
                    <div
                        :key="'create-character'"
                        v-if="dialogState.dialogType == 'create-character'">
                        <CampaignCreatePlayerCharacterForm
                            :onSubmit="
                                (req) =>
                                    campaignStore
                                        .createPlayerCharacter(req)
                                        .then(
                                            () =>
                                                (dialogState.dialogOpen = false)
                                        )
                            " />
                    </div>
                    <div
                        v-else-if="dialogState.dialogType === 'edit-character'">
                        <CampaignEditPlayerCharacterForm
                            ref="editCharacterForm"
                            :character="dialogState.characterClicked!"
                            :onEdit="editCharacter"
                            :onDelete="() => deleteCharacter(dialogState.characterClicked?.id!)" />
                    </div>
                </Transition>
            </DialogContent>
        </Dialog>
    </div>
</template>
<script setup lang="ts">
    import { faPerson, faPlusCircle } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import type { PlayerCharacterDto } from "~/utils/api/campaign/createPlayerCharacterRequest";

    import type {
        CampaignMemberResource,
        PlayerCharacter,
    } from "~/utils/types/models";
    const campaignStore = useCampaignStore();
    const user = useUserStore();
    const isViewingCurrentUsersCharacters = computed(
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
    }>({
        dialogOpen: false,
        dialogType: "create-character",
        characterClicked: undefined,
    });

    function openDialog(dialogType: DialogType) {
        dialogState.dialogType = dialogType;
        dialogState.dialogOpen = true;
    }

    async function editCharacter(playerCharacter: PlayerCharacterDto) {
        return campaignStore
            .updatePlayerCharacter(
                dialogState.characterClicked?.id!,
                playerCharacter
            )
            .then(() => (dialogState.dialogOpen = false));
    }

    async function deleteCharacter(characterId: string) {
        return campaignStore
            .deletePlayerCharacter(characterId)
            .then(() => (dialogState.dialogOpen = false));
    }

    const editCharacterForm = useTemplateRef("editCharacterForm");
    function trySubmitThenClose() {
        editCharacterForm.value?.onSubmit();
    }
</script>
