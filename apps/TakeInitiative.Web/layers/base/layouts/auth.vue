<template>
    <main
        class="flex h-full w-full flex-col overflow-auto bg-take-purple-very-dark text-take-grey-light"
    >
        <main class="bg-take-purple-very-dark">
            <section class="flex w-full justify-center p-4">
                <div
                    class="bg-take-neutral flex w-full flex-col items-center justify-center rounded-lg border border-take-yellow bg-take-navy-medium px-12 py-8 md:w-3/5 2xl:w-[700px]"
                >
                    <h1
                        class="flex items-center gap-2 font-NovaCut text-2xl font-bold text-take-yellow sm:text-3xl md:text-4xl"
                    >
                        <img
                            class="h-[1.5em] w-[1.5em]"
                            src="/img/yellowDice.png"
                        />
                        Take Initiative
                    </h1>
                    <div
                        class="my-1 w-full rounded-full border border-gray-400 opacity-50"
                    ></div>

                    <slot
                        v-if="status == 'success' && !data?.inMaintenanceMode"
                    />
                    <div
                        v-else-if="
                            status == 'success' && data?.inMaintenanceMode
                        "
                        class="border border-dashed border-take-red bg-take-red bg-opacity-35 p-2"
                    >
                        <label class="text-xl underline"
                            >In Maintenance Mode</label
                        >
                        <p>
                            Unfortunate Take Initiative is in maintenance mode,
                            but it should be back online soon.
                        </p>
                        <p>
                            {{ data.reason }}
                        </p>
                    </div>
                    <div v-else-if="status == 'pending'"></div>
                    <div v-else-if="status == 'error'">
                        {{ error }}
                    </div>
                </div>
            </section>
        </main>
    </main>
</template>
<script setup lang="ts">
// Check if in maintenance mode.
const { data, status, error } = await useAsyncData(
    "CheckMaintenanceMode",
    async () => {
        return useApi().admin.getMaintenance();
    },
);
</script>
