<template>
    <LoadingFallback :isLoading="campaignQuery.isLoading.value">
        <form @submit.prevent="onStage">
            <ul
                v-if="
                    campaignQuery.data.value?.userCampaignMember.characters
                        .length
                "
                class="flex flex-col gap-2">
                <li
                    v-for="character in campaignQuery.data.value
                        ?.userCampaignMember.characters"
                    :key="character.id"
                    @click="() => onClick(character.id)">
                    <Button
                        type="button"
                        class="interactable w-full h-fit justify-between"
                        :class="{
                            'border-primary': selectedCharacters.includes(
                                character.id
                            ),
                        }"
                        variant="outline">
                        <span>{{ character.name }}</span>
                        <CampaignCombatCharacterStatsDisplay
                            :armourClass="character.armourClass"
                            :health="character.health"
                            :initiative="character.initiative.roll"/>
                    </Button>
                </li>
            </ul>
            <ul v-else>
                <SheetDescription>
                    Looks like you don't have any characters in this campaign. You can pre-make characters for quick use
                    on the homepage of the campaign.
                </SheetDescription>
            </ul>
            <footer class="mt-4">
                <Button
                    type="submit"
                    :disabled="
                        stagePlayerCharactersMutation.isPending.value ||
                        selectedCharacters.length == 0
                    ">
                    <FontAwesomeIcon :icon="faPlusCircle"/>
                    {{
                        stagePlayerCharactersMutation.isIdle.value ||
                        stagePlayerCharactersMutation.isSuccess.value
                            ? "Add Selected"
                            : "Adding"
                    }}
                </Button>
            </footer>
        </form>
    </LoadingFallback>
</template>
<script setup lang="ts">
import {faPlusCircle} from "@fortawesome/free-solid-svg-icons";
import {FontAwesomeIcon} from "@fortawesome/vue-fontawesome";
import {useQuery} from "@tanstack/vue-query";
import {toast} from "vue-sonner";
import LoadingFallback from "~/components/LoadingFallback.vue";
import {getCampaignQuery} from "~/utils/queries/campaign";
import {useStagePlayerCharacterMutation} from "~/utils/queries/combats";

const props = defineProps<{
    campaignId: string;
    combatId: string;
}>();

const emits = defineEmits<{
    submitted: [];
}>();

const selectedCharacters = ref<string[]>([]);

function onClick(id: string) {
    if (selectedCharacters.value.includes(id)) {
        selectedCharacters.value = selectedCharacters.value.filter(
            (x) => x != id
        );
    } else {
        selectedCharacters.value.push(id);
    }
}

const campaignQuery = useQuery(getCampaignQuery(() => props.campaignId));
const stagePlayerCharactersMutation = useStagePlayerCharacterMutation();

async function onStage() {
    await stagePlayerCharactersMutation.mutateAsync({
        combatId: props.combatId,
        characterIds: selectedCharacters.value,
    });
    emits("submitted");
    toast.success("Added selected characters");
}
</script>
