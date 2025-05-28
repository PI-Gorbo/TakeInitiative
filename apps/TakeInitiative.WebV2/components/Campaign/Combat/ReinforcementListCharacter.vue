<template>
    <li class="pb-1 flex gap-1">
        <Button
            variant="outline"
            class="interactable w-full flex justify-between h-fit flex-1s"
            @click="emits('addStagedCharacter')">
            <div class="flex flex-col items-start">
                <label>{{ props.dto.character.name }}</label>
                <label class="text-muted-foreground">{{
                    props.dto.user.username
                }}</label>
            </div>
            <CampaignCombatCharacterStatsDisplay
                :armourClass="dto.character.armourClass"
                :health="dto.character.health"
                :initiative="dto.character.initiative.roll" />
        </Button>
        <div>
            <Sheet>
                <SheetTrigger>
                    <Button
                        variant="outline"
                        class="interactable w-fit h-full">
                        <FontAwesomeIcon :icon="faPencil" />
                    </Button>
                </SheetTrigger>
            </Sheet>
        </div>
    </li>
</template>

<script setup lang="ts">
    import { faPencil } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const emits = defineEmits<{
        addStagedCharacter: [];
    }>();

    const props = defineProps<{
        dto: StagedPlayerDto;
    }>();
</script>
