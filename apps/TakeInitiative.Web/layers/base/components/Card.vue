<template>
    <article
        class="flex justify-center">
        <section class="rounded bg-take-purple-dark px-5 py-2 md:w-2/3">
            <header class="w-full py-2">
                <h2 class="text-lg">{{ props.header }}</h2>
                <p class="text-sm text-take-grey">
                    {{props.subtitle}}
                </p>
            </header>
            <slot/>
        </section>
    </article>
</template>
<script setup lang="ts">

const props = defineProps<{
    header: string
    subtitle: string
}>()

</script>