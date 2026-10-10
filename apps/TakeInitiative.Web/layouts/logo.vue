<template>
    <div class="w-full h-full">
        <!-- Not wrapped in the default layout: the layout transition can only animate an
             element root, not a nested <NuxtLayout>. So its Toaster is repeated below. -->
        <div
            class="flex h-full w-full flex-col items-center pb-safe pt-safe px-safe">
            <div class="w-page-slim flex flex-col pt-10 sm:pt-20">
                <header>
                    <NuxtLink to="/">
                        <h1
                            class="flex items-center justify-center gap-4 font-NovaCut text-4xl font-bold text-gold sm:text-6xl">
                            <img
                                class="h-[2em] w-[2em]"
                                src="/yellowDice.png" />
                            Take Initiative
                        </h1>
                    </NuxtLink>
                </header>
                <div class="flex-1 px-4">
                    <slot />
                </div>
            </div>
        </div>
        <ClientOnly>
            <Toaster
                :position="'top-right'"
                :duration="1000" />
        </ClientOnly>
    </div>
</template>
<script setup lang="ts"></script>
