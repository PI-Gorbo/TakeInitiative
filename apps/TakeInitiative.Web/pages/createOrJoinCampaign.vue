<template>
    <main>
        <h1 class="flex m-2 justify-center">
            Looks like you aren't apart of, or own any campaigns yet!
        </h1>
        <Tabs notSelectedTabColour="take-navy-medium">
            <template #Create>
                <CreateCampaignForm />
            </template>
            <template #Join>
                <JoinCampaignForm />
            </template>
        </Tabs>
    </main>
</template>

<script setup lang="ts">
definePageMeta({ requiresAuth: true, layout: "auth" });
</script>
