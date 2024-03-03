<template>
    <ul class="max-h-full overflow-y-auto">
        <li
            v-for="member in props.campaignMemberDtos"
            :key="member.userId"
            class="mt-2 flex items-center justify-between gap-2 rounded-lg bg-take-navy-light bg-opacity-70 px-3 py-2 text-center"
        >
            <div class="flex items-center gap-2 text-center">
                <FontAwesomeIcon
                    class="text-take-yellow"
                    :icon="!member.isDungeonMaster ? 'user-large' : 'crown'"
                />
                <label class="select-none">{{ member.username }}</label>
            </div>
            <label class="select-none truncate">{{
                member.currentCharacter?.name
            }}</label>

            <div class="cursor-pointer">
                <FontAwesomeIcon icon="fa-ellipsis" />
            </div>
        </li>
    </ul>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type { CampaignMemberDto } from "~/utils/api/campaign/getCampaignRequest";

const props = withDefaults(
    defineProps<{
        campaignMemberDtos: CampaignMemberDto[];
    }>(),
    {}
);
</script>
