<template>
    <li class="pb-1 flex gap-1">
        <Button
            variant="outline"
            class="w-full flex justify-between h-fit flex-1 disabled:opacity-100 flex-wrap"
            :class="{
                interactable: userIsDmOrCharacterOwner,
            }"
            :disabled="!userIsDmOrCharacterOwner"
            @click="emits('addStagedCharacter')">
            <div class="flex flex-col items-start text-wrap">
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
        <div v-if="userIsDmOrCharacterOwner">
            <Sheet v-model:open="sheetIsOpen">
                <SheetTrigger asChild>
                    <Button
                        variant="outline"
                        class="interactable w-fit h-full">
                        <FontAwesomeIcon :icon="faPencil" />
                    </Button>
                </SheetTrigger>
                <SheetContent>
                    <CampaignCombatStageOrModifyCharacterForm
                        :character="props.dto.character"
                        :combatId="store.combat?.id!"
                        @submitted="() => (sheetIsOpen = false)" />
                </SheetContent>
            </Sheet>
        </div>
    </li>
</template>

<script setup lang="ts">
    import { faPencil } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    const sheetIsOpen = ref(false);
    const userStore = useUserStore();
    const store = useCombatStore();
    const emits = defineEmits<{
        addStagedCharacter: [];
    }>();

    const props = defineProps<{
        dto: StagedPlayerDto;
    }>();

    const userIsDmOrCharacterOwner = computed(() => {
        return (
            store.userIsDm || props.dto.user.userId === userStore.state?.userId
        );
    });
</script>
