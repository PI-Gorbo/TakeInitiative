<template>
    <main class="flex h-full flex-col bg-take-purple-very-dark">
        <header
            class="navbar border-b border-b-take-yellow bg-take-purple-dark"
        >
            <section class="navbar-start flex gap-2">
                <label class="flex items-center gap-2">
                    <img class="h-[3em] w-[3em]" src="/img/yellowDice.png" />
                    <label
                        class="select-none font-NovaCut text-xl text-take-yellow md:text-3xl lg:text-2xl"
                        >Take Initiative</label
                    >
                </label>
            </section>
            <section class="navbar-end flex items-center gap-2">
                <a class="cursor-pointer" href="https://discord.gg/caDetpm6vk">
                    <FontAwesomeIcon
                        :icon="faDiscord"
                        class="text-xl text-white"
                    />
                </a>
                <FormButton
                    label="Login"
                    @clicked="() => useNavigator().toLogin()"
                />
            </section>
        </header>
        <main class="hero flex-1 bg-take-purple-very-dark">
            <div class="hero-content text-center">
                <div class="flex flex-col gap-5">
                    <h1
                        class="font-NovaCut text-5xl font-bold text-take-yellow md:text-7xl"
                    >
                        Take Initiative
                    </h1>
                    <p class="max-w-lg text-take-grey">
                        Make in-person tabletop games easy with Take Initiative
                        - A tool for Dungeon Masters to plan combats and run
                        initiative live for your players.
                    </p>
                    <button
                        class="btn bg-take-teal text-lg text-white transition-colors hover:bg-take-teal hover:brightness-90 md:text-xl"
                        @click="() => useNavigator().toSignUp()"
                        :class="{}"
                    >
                        <span v-if="!userIsLoggedIn">Sign Up for Free</span>
                        <span v-else>Continue to your campaigns</span>
                    </button>
                </div>
            </div>
        </main>
        <!-- <section class="flex justify-center p-4">
            <ul class="grid grid-cols-5 gap-4 p-2">
                <li class="bg-take-creme-medium rounded p-2 text-take-navy">
                    <header class="text-xl">
                        CREATE CAMPAIGNS FOR YOU AND YOUR PLAYERS
                    </header>
                    <p>Invite your friends | play</p>
                </li>
                <li class="bg-take-creme-medium rounded p-2 text-take-navy">
                    <header class="text-xl">
                        PLAN AND RUN COMBATS IN ONE PLACE
                    </header>
                    <p>
                        Streamline your campaign's combats with a dedicated
                        space to plan and execute epic battles. Whether it's a
                        simple skirmish or a multi-stage showdown, prepare
                        everything in advance and kick off the combat at the
                        perfect moment! With your plans in place, you’re ready
                        to pivot and improvise as the adventure unfolds.
                    </p>
                </li>
                <li class="bg-take-creme-medium rounded p-2 text-take-navy">
                    <header class="text-xl">
                        COMBATS ARE LIVE ONLINE LOBBIES
                    </header>
                    <p>Invite your friends | play</p>
                </li>
                <li class="bg-take-creme-medium rounded p-2 text-take-navy">
                    <header class="text-xl">SPEED UP YOUR COMBATS</header>
                    <p>Invite your friends | play</p>
                </li>
                <li class="bg-take-creme-medium rounded p-2 text-take-navy">
                    <header class="text-xl">MORE FEATURES COMING SOON</header>
                    <p>Invite your friends | play</p>
                </li>
            </ul>
        </section> -->
    </main>
</template>

<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import { faDiscord } from "@fortawesome/free-brands-svg-icons";

const userStore = useUserStore();
const { data: userIsLoggedIn } = useAsyncData(
    "homePage-CheckUserIsLoggedIn",
    userStore.isLoggedIn,
);

useHead({
    title: "Take Initiative",
});

definePageMeta({
    layout: false,
    requiresAuth: false,
});
</script>
