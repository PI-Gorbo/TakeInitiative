<template>
    <LoadingFallback
        container="main"
        :isLoading="campaignQuery.isLoading.value"
        class="">
        <div class="w-full flex flex-col gap-4 pb-2 lg:max-w-md">
            <Card class="p-4 border-primary/50">
                <header><FontAwesomeIcon :icon="faUsers" /> Members</header>
                <ul class="flex flex-col gap-2 pt-2">
                    <li
                        v-for="member in membersToDisplay"
                        :key="member.memberId"
                        class="flex items-center gap-2">
                        <FontAwesomeIcon
                            :class="member.isOwner ? 'text-gold' : 'text-primary'"
                            :icon="member.isOwner ? faCrown : faUserLarge" />
                        <span class="select-none">{{ member.username }}</span>
                        <Badge :variant="member.role === 'DM' ? 'default' : 'secondary'">
                            {{ member.role }}
                        </Badge>
                        <AsyncButton
                            v-if="callerIsOwner && !member.isOwner"
                            class="ml-auto"
                            size="sm"
                            variant="outline"
                            :label="member.role === 'DM' ? 'Make Player' : 'Make DM'"
                            loadingLabel="Saving..."
                            :click="() => toggleRole(member)" />
                    </li>
                </ul>
            </Card>
        </div>
    </LoadingFallback>
</template>
<script setup lang="ts">
    import {
        faCrown,
        faUserLarge,
        faUsers,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useQuery } from "@tanstack/vue-query";
    import { toast } from "vue-sonner";
    import {
        getCampaignQuery,
        putMemberRoleMutation,
    } from "~/utils/queries/campaign";
    import { currentMember, type CampaignMember } from "~/utils/types/models";

    const route = useRoute("app-campaigns-campaignId");
    const campaignQuery = useQuery(
        getCampaignQuery(() => route.params.campaignId as string)
    );

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });

    const callerIsOwner = computed(
        () => currentMember(campaignQuery.data.value)?.isOwner ?? false
    );

    // The caller first, then the owner, then DMs, then alphabetically.
    const membersToDisplay = computed(() => {
        const campaign = campaignQuery.data.value;
        if (!campaign) {
            return [];
        }

        const rank = (m: CampaignMember) =>
            m.memberId === campaign.currentMemberId
                ? 0
                : m.isOwner
                  ? 1
                  : m.role === "DM"
                    ? 2
                    : 3;
        return [...campaign.members].sort(
            (a, b) => rank(a) - rank(b) || a.username.localeCompare(b.username)
        );
    });

    const putMemberRole = putMemberRoleMutation();
    async function toggleRole(member: CampaignMember) {
        await putMemberRole
            .mutateAsync({
                campaignId: route.params.campaignId as string,
                memberId: member.memberId,
                role: member.role === "DM" ? "Player" : "DM",
            })
            .catch(() => toast.error("Could not change the member's role."));
    }
</script>
