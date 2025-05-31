<template>
    <div class="flex h-full w-full flex-col">
        <slot />
        <!-- Nuxt  -->
        <ClientOnly>
            <Toaster :position="'top-right'" />
        </ClientOnly>
    </div>
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
