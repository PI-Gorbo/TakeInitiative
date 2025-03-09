<template>
    <main class="flex flex-col gap-5 items-center w-full h-full bg-background bg-grid">
        <section class="py-20 flex flex-col justify-center gap-4">
            <h1 class="font-NovaCut text-5xl font-bold text-gold md:text-7xl">
                Take Initiative
            </h1>
            <p class="max-w-lg text-center text-lg">
                Focus on the Story, Not the Stats <br /> Make Initiative Tracking a problem of the past
            </p>

            <Button>
                <span v-if="!userIsLoggedIn">Sign Up for Free</span>
                <span v-else>Continue to your campaigns</span>
            </Button>
        </section>

    </main>
</template>

<script setup lang="ts">
import Button from '~/components/ui/button/Button.vue';


</script>


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
        linear-gradient(to bottom, hsl(var(--primary) / 0.2) 1px, transparent 1px),
        linear-gradient(to right, hsl(var(--primary) / 0.2) 1px, transparent 1px);
    background-size: 50px 50px;
    /* Adjust grid size here */
    animation: scrollGrid 6s linear infinite;
    /* Adjust animation speed here */
    background-color: #121212;
    /* Or your desired background color */
}
</style>