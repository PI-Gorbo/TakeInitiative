<template>
    <main class="flex flex-col gap-4 pt-10">
        <Card class="border-2 border-dashed p-4">
            <CampaignEditIntroductionForm />
        </Card>
    </main>
</template>
<script setup lang="ts">
    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });
</script>
