<template>
    <div class="flex flex-col gap-4">
        <section>
            <header>Characters</header>
            <div class="px-2">
                <template v-if="(props.characters ?? []).length === 0">
                    <div
                        v-if="isViewingCurrentUsersCharacters"
                        class="flex flex-1 items-center justify-between">
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
                            class="interactable shadow-solid-sm"
                            :disabled="!isViewingCurrentUsersCharacters">
                            <FontAwesomeIcon :icon="faPerson" />
                            {{ character.name }}
                        </Button>
                        <div class="flex justify-end">
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
                    class="flex flex-1 items-center justify-between px-2">
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
        <Dialog v-model:open="dialogState.dialogOpen">
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>
                        {{
                            dialogState.dialogType == "create-character"
                                ? "Create a Character"
                                : "Share a resource"
                        }}
                    </DialogTitle>
                </DialogHeader>
                <template
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
                </template>
            </DialogContent>
        </Dialog>
    </div>
</template>
<script setup lang="ts">
    import { faPerson, faPlusCircle } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
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

    const dialogState = reactive<{
        dialogOpen: boolean;
        dialogType: "create-character" | "create-resource";
    }>({
        dialogOpen: false,
        dialogType: "create-character",
    });
    function openDialog(dialogType: "create-character" | "create-resource") {
        dialogState.dialogType = dialogType;
        dialogState.dialogOpen = true;
    }
</script>
