<template>
    <main class="p-2">
        <Card
            v-if="
                campaignMember?.characters == null ||
                campaignMember.characters.length == 0
            "
            header="Create your first character"
            subtitle="Characters can be quickly added to combats later!">
            <IndexModifyPlayerCharacterForm
                :onCreate="
                    (req: PlayerCharacterDto) =>
                        campaignStore.createPlayerCharacter(req)
                " />
        </Card>

        <ul v-else class="flex flex-col gap-2">
            <li
                v-for="character in campaignMember.characters"
                class="flex cursor-pointer gap-2"
                @click="() => showEditCharacterModal(character)">
                <IndexPlayerCharacterDisplay :character="character" />
            </li>

            <li
                class="group flex w-full cursor-pointer items-center justify-center rounded-xl border-2 border-dashed border-take-navy-light transition-colors hover:border-take-yellow"
                @click="showCreateCharacterModal">
                <FormButton
                    class="group-hover:text-take-yellow"
                    icon="plus"
                    label="New Character"
                    buttonColour="take-navy"
                    hoverButtonColour="take-navy"
                    textColour="white"
                    hoverTextColour="take-yellow"
                    @clicked="showCreateCharacterModal" />
            </li>
        </ul>
        <Modal ref="editCharacterModal" title="Edit Character">
            <IndexModifyPlayerCharacterForm
                v-if="lastClickedCharacter"
                :npc="lastClickedCharacter"
                :onEdit="
                    (req) =>
                        campaignStore
                            .updatePlayerCharacter(
                                lastClickedCharacter?.id!,
                                req
                            )
                            .then(hideEditCharacterModal)
                "
                :onDelete="
                    () =>
                        campaignStore
                            .deletePlayerCharacter(lastClickedCharacter?.id!)
                            .then(hideEditCharacterModal)
                " />
        </Modal>
        <Modal ref="createCharacterModal" title="Create new Character">
            <IndexModifyPlayerCharacterForm
                :onCreate="
                    (req: PlayerCharacterDto) =>
                        campaignStore
                            .createPlayerCharacter(req)
                            .then(hideCreateCharacterModal)
                " />
        </Modal>
    </main>
</template>
<script setup lang="ts">
    import type { PlayerCharacterDto } from "base/utils/api/campaign/createPlayerCharacterRequest";
    import Modal from "base/components/Modal.vue";
    import type { PlayerCharacter } from "base/utils/types/models";

    definePageMeta({
        requiresAuth: true,
        layout: "campaign-tabs",
    });

    const campaignStore = useCampaignStore();
    const campaignMember = computed(
        () => campaignStore.state.userCampaignMember
    );

    const editCharacterModal = ref<InstanceType<typeof Modal> | null>(null);
    const lastClickedCharacter = ref<PlayerCharacter | null>(null);
    function showEditCharacterModal(character: PlayerCharacter) {
        lastClickedCharacter.value = character;
        editCharacterModal.value?.show();
    }
    function hideEditCharacterModal() {
        editCharacterModal.value?.hide();
        lastClickedCharacter.value = null;
    }

    const createCharacterModal = ref<InstanceType<typeof Modal> | null>(null);
    function showCreateCharacterModal() {
        createCharacterModal.value?.show();
    }
    function hideCreateCharacterModal() {
        createCharacterModal.value?.hide();
    }
</script>
