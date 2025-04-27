<template>
    <Tabs>
        <TabsList>
            <TabsTrigger value="My Characters">My Characters</TabsTrigger>
            <TabsTrigger value="Draft Stages">Draft Stages</TabsTrigger>
            <TabsTrigger value="New Character">New Character</TabsTrigger>
        </TabsList>
        <TabsContent value="My Characters">
            <StageMyCharactersForm
                :campaignId="props.campaignId"
                :combatId="props.combatId"
                @submitted="() => emit('submitted')" />
        </TabsContent>
        <TabsContent value="Draft Stages">B</TabsContent>
        <TabsContent value="New Character">C</TabsContent>
    </Tabs>
</template>
<script setup lang="ts">
    import Tabs from "~/components/ui/tabs/Tabs.vue";
    import TabsContent from "~/components/ui/tabs/TabsContent.vue";
    import TabsList from "~/components/ui/tabs/TabsList.vue";
    import StageMyCharactersForm from "./StageMyCharactersForm.vue";

    const props = defineProps<{
        campaignId: string;
        combatId: string;
    }>();

    const emit = defineEmits<{
        submitted: [];
    }>();
</script>
