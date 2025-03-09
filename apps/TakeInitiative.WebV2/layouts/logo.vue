<template>
    <div class="w-full h-full">
        <NuxtLayout name="default">
            <div class="flex h-full w-full flex-col items-center pt-20">
                <div class="w-page-slim">
                    <header>
                        <NuxtLink to="/">
                            <h1
                                class="flex items-center justify-center gap-4 font-NovaCut text-4xl font-bold text-gold sm:text-6xl">
                                <img class="h-[2em] w-[2em]" src="~/public/img/yellowDice.png" />
                                Take Initiative
                            </h1>
                        </NuxtLink>
                    </header>
                    <slot />
                </div>
            </div>
        </NuxtLayout>
    </div>
</template>
<script setup lang="ts"></script>