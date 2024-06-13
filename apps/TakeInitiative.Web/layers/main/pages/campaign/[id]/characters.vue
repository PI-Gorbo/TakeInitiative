<template>
    <main class="px-2 py-2 sm:px-0">
        <div
            v-if="
                campaignMember?.characters == null ||
                campaignMember.characters.length == 0
            "
            class="flex justify-center"
        >
            <section class="md:w-2/3">
                <label
                    class="flex w-full justify-center font-NovaCut text-lg text-take-yellow"
                    >Looks like you don't have any characters yet. Create
                    one!</label
                >
                <IndexModifyPlayerCharacterForm
                    :onCreate="
                        (req: PlayerCharacterDto) =>
                            campaignStore.createPlayerCharacter(req)
                    "
                />
            </section>
        </div>
        <ul v-else class="flex flex-col gap-2">
            <li
                v-for="character in campaignMember.characters"
                class="flex cursor-pointer gap-2"
                @click="() => showEditCharacterModal(character)"
            >
                <IndexPlayerCharacterDisplay :character="character" />
            </li>

            <li
                class="group flex w-full cursor-pointer items-center justify-center rounded-xl border-2 border-dashed border-take-navy-light transition-colors hover:border-take-yellow"
                @click="showCreateCharacterModal"
            >
                <FormButton
                    class="group-hover:text-take-yellow"
                    icon="plus"
                    label="New Character"
                    buttonColour="take-navy"
                    hoverButtonColour="take-navy"
                    textColour="white"
                    hoverTextColour="take-yellow"
                    @clicked="showCreateCharacterModal"
                />
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
                                req,
                            )
                            .then(hideEditCharacterModal)
                "
                :onDelete="
                    () =>
                        campaignStore
                            .deletePlayerCharacter(lastClickedCharacter?.id!)
                            .then(hideEditCharacterModal)
                "
            />
        </Modal>
        <Modal ref="createCharacterModal" title="Create new Character">
            <IndexModifyPlayerCharacterForm
                :onCreate="
                    (req: PlayerCharacterDto) =>
                        campaignStore
                            .createPlayerCharacter(req)
                            .then(hideCreateCharacterModal)
                "
            />
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
const campaignMember = computed(() => campaignStore.state.userCampaignMember);

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
