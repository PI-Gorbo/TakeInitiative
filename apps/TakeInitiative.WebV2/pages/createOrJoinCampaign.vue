<template>
    <Card>
        <CardHeader>
            <CardTitle>Create a new campaign or join one with a code</CardTitle>
        </CardHeader>
        <CardContent>
            <Tabs default-value="Create">
                <TabsList class="grid w-full grid-cols-2">
                    <TabsTrigger value="Create"> Create </TabsTrigger>
                    <TabsTrigger value="Join"> Join </TabsTrigger>
                </TabsList>
                <TabsContent value="Create">
                    <CreateCampaignForm :submit="createCampaign" class="px-2" />
                </TabsContent>
                <TabsContent value="Join">
                    <JoinCampaignForm :submit="joinCampaign" class="px-2" />
                </TabsContent>
            </Tabs>
        </CardContent>
    </Card>
</template>

<script setup lang="ts">
    import CardTitle from "~/components/ui/card/CardTitle.vue";
    import Tabs from "~/components/ui/tabs/Tabs.vue";
    import type { CreateCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
    import type { JoinCampaignRequest } from "~/utils/api/campaign/joinCampaignRequest";

    definePageMeta({ requiresAuth: true });
    const userStore = useUserStore();
    async function createCampaign(req: CreateCampaignRequest) {
        return await userStore
            .createCampaign(req)
            .then(
                async (c) => await useNavigator().toCampaignTab(c.id, "summary")
            );
    }

    async function joinCampaign(req: JoinCampaignRequest) {
        return await useUserStore()
            .joinCampaign(req)
            .then(
                async (c) => await useNavigator().toCampaignTab(c.id, "summary")
            );
    }
</script>
