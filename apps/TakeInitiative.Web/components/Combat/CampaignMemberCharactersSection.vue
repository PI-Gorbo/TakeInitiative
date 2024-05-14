<template>
    <main>
        <ul v-if="characters">
            <li
                v-for="character in characters"
                :key="character.id"
                @click="() => onClick(character.id)"
            >
                <IndexPlayerCharacterDisplay
                    :character="character"
                    :class="[
                        selectedCharacters.includes(character.id) &&
                            'border-take-yellow',
                    ]"
                />
            </li>
        </ul>
        <footer class="mt-4">
            <FormButton
                :disabled="selectedCharacters.length == 0"
                label="Stage"
                :loadingDisplay="{
                    showSpinner: true,
                    loadingText: 'Staging...',
                }"
                buttonColour="take-yellow"
                hoverButtonColour="take-yellow-dark"
                :click="() => onStage(selectedCharacters)"
            />
        </footer>
    </main>
</template>
<script setup lang="ts">
const selectedCharacters = ref<string[]>([]);
function onClick(id: string) {
    if (selectedCharacters.value.includes(id)) {
        selectedCharacters.value = selectedCharacters.value.filter(
            (x) => x != id,
        );
    } else {
        selectedCharacters.value.push(id);
    }
}

const campaignStore = useCampaignStore();
const characters = computed(
    () => campaignStore.state.userCampaignMember?.characters,
);

const props = defineProps<{
    onStage: (playerCharacterIds: string[]) => Promise<unknown>;
}>();
</script>
