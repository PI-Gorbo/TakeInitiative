<template>
    <main class="flex flex-col gap-4">
        <section class="flex w-full flex-col justify-center gap-6">
            <p class="w-full text-center">
                Focus on the Story, Not the Stats - Make Initiative Tracking a
                problem of the past
            </p>
            <div class="flex flex-col gap-2">
                <template v-if="resumingSession">
                    <Skeleton class="h-9 w-full" />
                    <Skeleton class="h-9 w-full" />
                </template>
                <template v-else-if="!userStore.isLoggedIn">
                    <NuxtLink
                        class="w-full"
                        to="/signup">
                        <Button class="w-full">
                            <span>Sign up for free</span>
                        </Button>
                    </NuxtLink>
                    <NuxtLink
                        class="w-full"
                        to="/login">
                        <Button
                            class="w-full"
                            variant="outline">
                            Got an account? Login
                        </Button>
                    </NuxtLink>
                </template>
                <template v-else>
                    <NuxtLink
                        class="w-full"
                        to="/app/campaigns">
                        <Button class="w-full"> Go to your Campaigns</Button>
                    </NuxtLink>
                </template>
            </div>
        </section>

        <section class="rounded-md border bg-background p-2">
            <a
                class="cursor-pointer"
                href="https://discord.gg/caDetpm6vk">
                <header class="cursor-pointer font-NovaCut text-gold">
                    <FontAwesomeIcon
                        :icon="faDiscord"
                        class="text-white" />

                    Join us on discord!
                </header>
                <p class="cursor-pointer">
                    Tell us about any bugs, or request new features on discord.
                </p>
            </a>
        </section>
    </main>
</template>

<script setup lang="ts">
    import { faDiscord } from "@fortawesome/free-brands-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import Button from "~/components/ui/button/Button.vue";
    import Skeleton from "~/components/ui/skeleton/Skeleton.vue";

    const userStore = useUserStore();

    // `checkAuth.global` skips `/`, so a returning visitor's session is resumed here instead.
    // The auth cookie is HttpOnly: only the server render can see it, and `useState` carries
    // the answer across hydration.
    const hasSessionCookie = useState("homeHasSessionCookie", () =>
        hasAuthCookie(useRequestHeaders(["cookie"]).cookie)
    );

    // Server-rendered as loading, so the anonymous buttons never flash for a returning visitor.
    const resumingSession = ref(hasSessionCookie.value);

    onMounted(async () => {
        if (!resumingSession.value) {
            return;
        }

        // A stale or revoked cookie just 401s, which leaves the user null.
        await userStore.init().catch(() => {});
        resumingSession.value = false;

        if (userStore.isLoggedIn) {
            await useNavigator().toCampaignsList();
        }
    });

    useHead({
        title: "Take Initiative",
    });

    definePageMeta({
        requiresAuth: false,
        layout: "logo",
    });
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
