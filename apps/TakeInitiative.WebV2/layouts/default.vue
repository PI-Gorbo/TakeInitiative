
<template>
    <main class="flex flex-col w-full h-full">
        <header class="border-b border-b-gold bg-primary/5 border-double p-2 flex justify-between">
            <section class="flex gap-2">
                <label class="flex items-center gap-2">
                    <img class="h-[3em] w-[3em]" src="~/assets/yellowDice.png" />
                    <label class="select-none font-NovaCut text-xl text-gold md:text-3xl lg:text-2xl">Take
                        Initiative</label>
                </label>
            </section>
            <section class="flex items-center px-2">
                <a class="cursor-pointer" href="https://discord.gg/caDetpm6vk">
                    <FontAwesomeIcon :icon="faDiscord" class="text-xl text-white" />
                </a>
                <!-- <FormButton label="Login" @clicked="toLogin" /> -->
            </section>
        </header>

        <NuxtPage />
    </main>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome';
import { faDiscord } from '@fortawesome/free-brands-svg-icons';

</script>
