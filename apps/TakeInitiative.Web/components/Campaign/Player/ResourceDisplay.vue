<template>
    <a
        :href="resource.link"
        target="_blank"
        class="h-fit flex justify-between w-full items-center">
        <div class="flex gap-1 items-center">
            <img
                height="24"
                width="24"
                :src="`http://www.google.com/s2/favicons?domain=${resource.link}`" />
            <span>{{ props.resource.name }}</span>
        </div>
        <div class="flex items-center gap-2">
            <Badge>
                {{ resourceVisibilityOptionNameMap[resource.visibility] }}
            </Badge>
           
        </div>
    </a>
</template>

<script setup lang="ts">
    import { faLink, faPen } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import {
        resourceVisibilityOptionNameMap,
        ResourceVisibilityOptions,
    } from "~/utils/types/models";

    const props = defineProps<{
        resource: {
            name: string;
            link: string;
            visibility: ResourceVisibilityOptions;
        };
    }>();
</script>
