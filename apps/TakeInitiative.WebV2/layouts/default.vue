<template>
    <main class="bg-grid flex h-full w-full flex-col bg-background">
        <div class="flex h-full w-full flex-col items-center pt-20">
            <div class="w-full max-w-2xl sm:w-4/5">
                <header>
                    <NuxtLink to="/">
                        <h1
                            class="flex items-center justify-center gap-4 font-NovaCut text-4xl font-bold text-gold sm:text-6xl">
                            <img
                                class="h-[2em] w-[2em]"
                                src="~/public/img/yellowDice.png" />
                            Take Initiative
                        </h1>
                    </NuxtLink>
                </header>
                <slot />
            </div>
        </div>
    </main>
</template>

<style scoped>
    @keyframes scrollGrid {
        0% {
            background-position: 0 0;
        }

        100% {
            background-position: 100px 100px;
            /* Match grid size here */
        }
    }

    .bg-grid {
        background-image:
            linear-gradient(
                to bottom,
                hsl(var(--primary) / 0.2) 1px,
                transparent 1px
            ),
            linear-gradient(
                to right,
                hsl(var(--primary) / 0.2) 1px,
                transparent 1px
            );
        background-size: 50px 50px;
        /* Adjust grid size here */
        animation: scrollGrid 6s linear infinite;
        /* Adjust animation speed here */
        background-color: #121212;
        /* Or your desired background color */
    }
</style>
