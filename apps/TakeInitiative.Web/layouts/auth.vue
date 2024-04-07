<template>
    <main
        class="flex h-full w-full flex-col overflow-auto bg-take-navy text-white"
    >
        <main class="bg-take-navy">
            <section class="flex w-full justify-center p-4 shadow-sm">
                <div
                    class="bg-take-neutral flex w-full flex-col items-center justify-center rounded-lg border border-take-yellow bg-take-navy-medium px-12 py-8 md:w-3/5 2xl:w-[700px]"
                >
                    <h1
                        class="flex items-center gap-2 font-NovaCut text-2xl font-bold text-take-yellow sm:text-3xl md:text-4xl"
                    >
                        <img
                            class="h-[1.5em] w-[1.5em]"
                            src="~/assets/yellowDice.png"
                        />
                        Take Initiative
                    </h1>
                    <div
                        class="my-1 w-full rounded-full border border-gray-400 opacity-50"
                    ></div>

                    <slot />
                </div>
            </section>
        </main>
    </main>
</template>
<script setup lang="ts"></script>
