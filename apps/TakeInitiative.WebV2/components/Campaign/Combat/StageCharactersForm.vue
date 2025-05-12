<template>
    <Tabs>
        <TabsList>
            <TabsTrigger value="My Characters">My Characters</TabsTrigger>
            <TabsTrigger v-if="props.userIsDm" value="Draft Stages"
                >Draft Stages</TabsTrigger
            >
            <TabsTrigger value="New Character">New Character</TabsTrigger>
        </TabsList>
        <TabsContent value="My Characters">
            <StageMyCharactersForm
                :campaignId="props.campaignId"
                :combatId="props.combatId"
                @submitted="() => emit('submitted')" />
        </TabsContent>
        <TabsContent value="Draft Stages">
            <CampaignCombatStagePlannedCharactersForm
                :plannedStages="plannedStages ?? []"
                :combatId="props.combatId"
                @submitted="() => emit('submitted')" />
        </TabsContent>
        <TabsContent value="New Character">C</TabsContent>
    </Tabs>
</template>
<script setup lang="ts">
    import Tabs from "~/components/ui/tabs/Tabs.vue";
    import TabsContent from "~/components/ui/tabs/TabsContent.vue";
    import TabsList from "~/components/ui/tabs/TabsList.vue";
    import StageMyCharactersForm from "./StageMyCharactersForm.vue";
    import type { GetCombatResponse } from "~/utils/api/combat/getCombatRequest";

    const props = defineProps<{
        campaignId: string;
        combatId: string;
        userIsDm: boolean;
        plannedStages: GetCombatResponse["combat"]["plannedStages"];
    }>();

    const emit = defineEmits<{
        submitted: [];
    }>();
</script>
