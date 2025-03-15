<template>
    <div class="drawer flex h-full bg-take-navy text-white">
        <main class="flex h-full w-full flex-col">
            <header
                class="navbar border border-take-navy-medium border-b-take-yellow bg-take-navy-medium"
            >
                <section class="navbar-start flex gap-2">
                    <label
                        for="drawer"
                        class="flex cursor-pointer items-center gap-2"
                    >
                        <img
                            class="h-[3em] w-[3em]"
                            src="~/public/img/yellowDice.png"
                        />
                        <label
                            class="font-NovaCut text-2xl text-take-yellow md:text-3xl"
                            >Take Initiative</label
                        >
                    </label>
                </section>
            </header>
            <div class="flex-1 overflow-y-scroll">
                <slot />
            </div>
        </main>
    </div>
</template>
<script setup lang="ts"></script>
