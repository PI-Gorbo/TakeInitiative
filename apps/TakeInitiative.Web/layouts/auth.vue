<template>
    <Default>
        <main class="bg-take-navy">
            <section class="shadow-sm p-4 w-full flex justify-center">
                <div
                    class="bg-take-navy-medium flex flex-col items-center rounded-lg justify-center px-12 py-8 bg-take-neutral border border-take-yellow w-3/5 2xl:w-[700px]"
                >
                    <h1
                        class="text-2xl font-bold text-take-yellow sm:text-3xl md:text-4xl font-NovaCut flex gap-2 items-center"
                    >
                        <img class="w-[1.5em] h-[1.5em]" src="~/assets/yellowDice.png" />
                        Take Initiative
                    </h1>
                    <div
                        class="border border-gray-400 opacity-50 w-full rounded-full my-1"
                    ></div>

                    <slot />
                </div>
            </section>
        </main>
    </Default>
</template>
<script setup lang="ts">
import Default from "./default.vue";
</script>
