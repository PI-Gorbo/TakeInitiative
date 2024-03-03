<template>
    <main class="flex h-full w-full flex-col overflow-auto bg-take-navy text-white">
        <header>
            <div
                class="flex select-none border-b-2 border-take-yellow bg-take-navy-medium px-4 py-1 justify-center"
            >
                <div class="sm:w-full md:w-4/5 lg:w-7/10 2xl:w-full max-w-[1200px] flex">
                    <h1
                        @click="() => navigateTo('/')"
                        class="flex cursor-pointer items-center gap-2 font-NovaCut text-2xl font-bold text-take-yellow sm:text-3xl md:text-4xl"
                    >
                        <img class="h-[1.5em] w-[1.5em]" src="~assets/yellowDice.png" />
                        Take Initiative
                    </h1>
                    <section class="flex flex-1 justify-end gap-2">
                        <div
                            class="flex items-center px-2 py-1 font-NovaCut text-xl text-take-yellow"
                        >
                            <label>{{ userStore.username }}</label>
                        </div>
                        <div class="flex items-center">
                            <FormButton
                                icon="share-from-square"
                                textColour="white"
                                size="sm"
                                buttonColour="take-navy-light"
                                hoverButtonColour="take-yellow"
                            />
                        </div>
                        <div class="flex items-center" @click.capture>
                            <FormButton
                                @clicked="() => navigateTo('/campaigns')"
                                icon="user-gear"
                                size="sm"
                                textColour="white"
                                buttonColour="take-navy-light"
                                hoverButtonColour="take-yellow"
                            />
                        </div>
                    </section>
                </div>
            </div>
        </header>

        <div class="flex-1 flex justify-center">
            <div class="sm:w-full md:w-4/5 lg:w-7/10 2xl:w-full max-w-[1200px]">
                <slot />
            </div>
        </div>
    </main>
</template>
<script setup lang="ts">
const userStore = useUserStore();
</script>
