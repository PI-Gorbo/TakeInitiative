<template>
    <Card>
        <CardContent class="flex flex-col">
            <CardHeader>
                <CardTitle
                    >Create a new campaign or join one with a code</CardTitle
                >
            </CardHeader>
            <Tabs default-value="Create" class="flex flex-col gap-4">
                <TabsList class="grid w-full grid-cols-2">
                    <TabsTrigger value="Create"> Create </TabsTrigger>
                    <TabsTrigger value="Join"> Join </TabsTrigger>
                </TabsList>
                <TabsContent value="Create">
                    <CampaignCreateForm :submit="createCampaign" class="px-2" />
                </TabsContent>
                <TabsContent value="Join">
                    <CampaignJoinForm :submit="joinCampaign" class="px-2" />
                </TabsContent>
            </Tabs>
        </CardContent>
    </Card>
</template>

<script setup lang="ts">
    import type { CreateCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
    import type { JoinCampaignRequest } from "~/utils/api/campaign/joinCampaignRequest";

    definePageMeta({ requiresAuth: true, layout: "logo" });
    const userStore = useUserStore();
    async function createCampaign(req: CreateCampaignRequest) {
        return await userStore
            .createCampaign(req)
            .then(async (c) => await useNavigator().toCampaign(c.id));
    }

    async function joinCampaign(req: JoinCampaignRequest) {
        return await useUserStore()
            .joinCampaign(req)
            .then(async (c) => await useNavigator().toCampaign(c.id));
    }
</script>
