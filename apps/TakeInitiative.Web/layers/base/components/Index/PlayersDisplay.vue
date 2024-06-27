<template>
    <ul class="max-h-full overflow-y-auto">
        <li
            v-for="member in membersToDisplay"
            :key="member.userId"
            class="mt-2 flex items-center justify-between gap-2 rounded-lg bg-take-purple bg-opacity-70 px-3 py-2 text-center"
        >
            <div class="flex items-center gap-2 text-center">
                <FontAwesomeIcon
                    :class="
                        member.isDungeonMaster
                            ? 'text-take-yellow'
                            : 'text-take-teal'
                    "
                    :icon="!member.isDungeonMaster ? 'user-large' : 'crown'"
                />
                <label class="select-none">{{ member.username }}</label>
            </div>

            <div class="cursor-pointer">
                <!-- <FontAwesomeIcon icon="fa-ellipsis" /> -->
            </div>
        </li>
    </ul>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type { CampaignMemberDto } from "base/utils/api/campaign/getCampaignRequest";

const props = withDefaults(
    defineProps<{
        campaignMemberDtos: CampaignMemberDto[];
    }>(),
    {},
);

const membersToDisplay = computed(() =>
    props.campaignMemberDtos.sort((a, b) => {
        if (a.isDungeonMaster && !b.isDungeonMaster) {
            return -1;
        }

        if (!a.isDungeonMaster && b.isDungeonMaster) {
            return 1;
        }

        if (a.username > b.username) {
            return -1;
        }

        return 1;
    }),
);
</script>
