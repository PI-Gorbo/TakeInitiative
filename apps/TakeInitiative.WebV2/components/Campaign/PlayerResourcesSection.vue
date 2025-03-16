<template>
    <div class="flex flex-col gap-4">
        <section>
            <header>Characters</header>
            <template v-if="(props.characters ?? []).length === 0">
                <div
                    v-if="isViewingCurrentUsersCharacters"
                    class="flex flex-1 items-center justify-between px-2">
                    <span class="text-gray-500">
                        You've not got any characters yet.
                    </span>
                    <Button variant="outline">
                        <FontAwesomeIcon :icon="faPlusCircle" />
                        New
                    </Button>
                </div>
                <div
                    v-else
                    class="flex flex-1 items-center justify-between px-2">
                    <span class="text-gray-500"> No characters yet. </span>
                </div>
            </template>
        </section>
        <section>
            <header>Resources</header>
            <template v-if="(props.resources ?? []).length == 0">
                <div
                    v-if="isViewingCurrentUsersCharacters"
                    class="flex flex-1 items-center justify-between px-2">
                    <span class="text-gray-500"
                        >You've not got any resources yet.</span
                    >
                    <Button variant="outline">
                        <FontAwesomeIcon :icon="faPlusCircle" />
                        New
                    </Button>
                </div>
                <div
                    v-else
                    class="flex flex-1 items-center justify-between px-2">
                    <span class="text-gray-500"> No resources yet. </span>
                </div>
            </template>
        </section>
    </div>
</template>
<script setup lang="ts">
    import { faPlusCircle } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import type {
        CampaignMemberResource,
        PlayerCharacter,
    } from "~/utils/types/models";

    const user = useUserStore();
    const isViewingCurrentUsersCharacters = computed(
        () => user.state.user?.userId == props.userId
    );

    const props = defineProps<{
        userId: string;
        resources: CampaignMemberResource[];
        characters: PlayerCharacter[];
    }>();
</script>
