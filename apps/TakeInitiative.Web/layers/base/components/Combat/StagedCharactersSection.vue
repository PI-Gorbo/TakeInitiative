<template>
    <main>
        <div
            class="flex flex-col gap-2"
            v-if="viewState.currentView.name == 'List'"
        >
            <label class="text-sm italic">
                A list of characters that the DM can add to the combat.</label
            >
            <div class="flex flex-col gap-2">
                <ul
                    v-for="character in orderedStagedCharacterListWithPlayerInfo"
                    :key="character.character.id"
                    class="flex flex-row items-center gap-2"
                >
                    <CombatStagedCharacterDisplay
                        :charInfo="character"
                        :class="[
                            'flex-1 select-none',
                            userIsDm && 'cursor-pointer',
                            clickedList.includes(character.character.id) &&
                                'border-take-yellow',
                        ]"
                        @click="() => onCharacterClicked(character)"
                    />
                    <FormButton
                        v-if="combatStore.isEditableForUser(character)"
                        icon="wrench"
                        size="sm"
                        buttonColour="take-navy-light"
                        class="h-min"
                        @clicked="
                            () => transitionViewToEdit(character.character)
                        "
                    />
                </ul>
            </div>
            <footer
                class="flex gap-4"
                :class="[userIsDm ? 'justify-between' : 'justify-end']"
            >
                <FormButton
                    v-if="userIsDm"
                    label="Roll into initiative"
                    :loadingDisplay="{
                        showSpinner: true,
                        loadingText: 'Rolling...',
                    }"
                    size="sm"
                    icon="dice"
                    :disabled="clickedList.length == 0"
                    :click="
                        () =>
                            combatStore
                                .rollIntoInitiative({
                                    characterIds: clickedList,
                                })
                                .then(() =>
                                    emit('RolledCharactersIntoInitiative'),
                                )
                    "
                />
                <FormButton
                    label="Stage character"
                    size="sm"
                    buttonColour="take-purple-light"
                    icon="plus"
                    @clicked="transitionViewToAdd"
                />
            </footer>
        </div>
        <div v-else-if="viewState.currentView.name == 'Edit'">
            <FormButton
                size="sm"
                icon="arrow-left"
                buttonColour="take-purple-light"
                label="Back"
                @clicked="transitionViewToList"
            />
            <CombatModifyStagedCharacterForm
                :onEdit="
                    (req) =>
                        combatStore
                            .updateStagedCharacter(req)
                            .then(transitionViewToList)
                "
                :onDelete="
                    (req) =>
                        combatStore
                            .deleteStagedCharacter(req)
                            .then(
                                () =>
                                    (clickedList = clickedList.filter(
                                        (x) => x != req.characterId,
                                    )),
                            )
                            .then(transitionViewToList)
                "
                :character="viewState.currentView.characterToEdit"
            />
        </div>
        <div v-else-if="viewState.currentView.name == 'Add'">
            <FormButton
                size="sm"
                icon="arrow-left"
                buttonColour="take-purple-light"
                label="Back to list"
                @clicked="transitionViewToList"
            />

            <CombatAddStagedCharacterTabs
                :characterAdded="async () => transitionViewToList()"
            />
        </div>
    </main>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type { InitiativeCharacter } from "base/utils/types/models";

const viewState = reactive<{
    currentView:
        | {
              name: "List";
          }
        | {
              name: "Edit";
              characterToEdit: InitiativeCharacter;
          }
        | {
              name: "Add";
          };
}>({
    currentView: {
        name: "List",
    },
});
function transitionViewToAdd() {
    viewState.currentView = {
        name: "Add",
    };
}
function transitionViewToList() {
    viewState.currentView = {
        name: "List",
    };
}
function transitionViewToEdit(characterToEdit: InitiativeCharacter) {
    viewState.currentView = {
        name: "Edit",
        characterToEdit,
    };
}

const clickedList = ref<string[]>([]);
const campaignStore = useCampaignStore();
const combatStore = useCombatStore();
const {
    userIsDm,
    orderedStagedCharacterListWithPlayerInfo,
    anyPlannedCharacters,
} = storeToRefs(combatStore);

function onCharacterClicked(character: CombatPlayerDto) {
    if (userIsDm.value) {
        clickedList.value.includes(character.character.id)
            ? (clickedList.value = clickedList.value.filter(
                  (x) => x != character.character.id,
              ))
            : clickedList.value.push(character.character.id);
    }
}

const emit = defineEmits<{
    (e: "RolledCharactersIntoInitiative"): void;
}>();

// A hook to set the currentView based on if there are any staged characters
onMounted(() => {
    if (orderedStagedCharacterListWithPlayerInfo.value.length != 0) {
        return;
    }

    transitionViewToAdd();
});
</script>
