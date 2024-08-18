<template>
    <body class="flex w-full flex-col items-center">
        <main
            key="Success"
            v-if="status == 'success'"
            class="flex w-full max-w-[1200px] flex-col items-center gap-2 p-2 md:w-4/5"
        >
            <section
                class="w-full rounded-lg border border-take-navy-medium p-2"
            >
                <label class="text-lg"
                    >Maintenance Mode -
                    {{
                        data?.maintenanceData?.inMaintenanceMode ? "ON" : "OFF"
                    }}</label
                >
                <p v-if="data?.maintenanceData?.inMaintenanceMode">
                    {{ data?.maintenanceData.reason }}
                </p>
                <FormBase
                    v-slot="{ submitting }"
                    v-if="!data?.maintenanceData?.inMaintenanceMode"
                    :onSubmit="onEnableMaintenanceMode"
                    class="flex flex-col gap-2"
                >
                    <FormInput
                        label="Reason"
                        v-model:value="reason"
                        v-bind="reasonInputProps"
                    />
                    <div class="flex justify-end">
                        <FormButton
                            buttonColour="take-red"
                            label="Enable Maintenance Mode"
                            :loadingDisplay="{
                                showSpinner: true,
                                loadingText: 'Enabling...',
                            }"
                            :isLoading="submitting"
                        />
                    </div>
                </FormBase>
                <div v-else class="flex w-full justify-end">
                    <FormButton
                        label="Disable Maintenance Mode"
                        :loadingDisplay="{
                            showSpinner: true,
                            loadingText: 'Enabling...',
                        }"
                        :click="onDisableMaintenanceMode"
                    />
                </div>
            </section>
            <section
                class="w-full rounded-lg border p-2"
                :class="{
                    'border-take-navy-medium':
                        data?.maintenanceData?.inMaintenanceMode,
                    'border-take-grey-dark':
                        !data?.maintenanceData?.inMaintenanceMode,
                }"
            >
                <label
                    class="text-lg"
                    :class="{
                        'text-take-grey':
                            !data?.maintenanceData?.inMaintenanceMode,
                    }"
                    >Reprojections</label
                >
                <div>
                    <FormButton
                        label="Combats"
                        :disabled="!data?.maintenanceData?.inMaintenanceMode"
                        :click="onReprojectCombats"
                        :loadingDisplay="{
                            showSpinner: true,
                            loadingText: 'Reprojecting Combats...',
                        }"
                    />
                </div>
            </section>
            <section
                class="w-full rounded-lg border p-2"
                :class="{
                    'border-take-navy-medium':
                        data?.maintenanceData?.inMaintenanceMode,
                    'border-take-grey-dark':
                        !data?.maintenanceData?.inMaintenanceMode,
                }"
            >
                <label
                    class="text-lg"
                    :class="{
                        'text-take-grey':
                            !data?.maintenanceData?.inMaintenanceMode,
                    }"
                    >Read And Save</label
                >
                <div>
                    <FormButton
                        label="Campaigns"
                        :disabled="!data?.maintenanceData?.inMaintenanceMode"
                        :click="onReadAndSaveCampaigns"
                        :loadingDisplay="{
                            showSpinner: true,
                            loadingText: 'Running...',
                        }"
                    />
                </div>
            </section>
        </main>
        <section v-else key="ErrorOrWaiting">
            {{ status }}
        </section>
    </body>
</template>
<script setup lang="ts">
import { toTypedSchema } from "@vee-validate/zod";
import { useForm } from "vee-validate";
import {
    MaintenanceConfigValidator,
    type MaintenanceConfig,
} from "base/utils/api/admin/getMaintainenceRequest";
import { useToast } from "vue-toastification";

definePageMeta({
    layout: "admin",
});

// Setup
const adminApi = useAdminApi();
const toast = useToast();

// Get maintenance status api call.
const { data, refresh, status } = await useAsyncData(
    "maintenanceQuery",
    async () => {
        const maintenanceData = await adminApi.getMaintenance();
        return {
            maintenanceData,
        };
    },
);

// Maintenance Mode form
const maintenanceModeForm = reactive<{
    error: ApiError<Omit<MaintenanceConfig, "isMaintenanceMode">> | null;
}>({
    error: null,
});
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        MaintenanceConfigValidator.omit(["inMaintenanceMode"]),
    ),
});
const [reason, reasonInputProps] = defineField("reason", {
    props: (state) => ({
        errorMessage:
            maintenanceModeForm.error == null
                ? state.errors[0]
                : maintenanceModeForm.error?.getErrorFor("reason"),
    }),
});

async function onEnableMaintenanceMode() {
    maintenanceModeForm.error = null;
    const validateResult = await validate();
    if (!validateResult.valid) {
        return Promise.resolve();
    }

    return await adminApi
        .putMaintenance({
            inMaintenanceMode: true,
            reason: reason.value!,
        })
        .then((inputData) => {
            if (data.value) return (data.value.maintenanceData = inputData);
        })
        .then(() => {
            toast.success("Success!");
        })
        .catch((e) => toast.error(`Error! ${e}`));
}
async function onDisableMaintenanceMode() {
    return await adminApi
        .putMaintenance({
            inMaintenanceMode: false,
            reason: null,
        })
        .then((inputData) => {
            if (data.value) return (data.value.maintenanceData = inputData);
        })
        .then(() => {
            toast.success("Success!");
        })
        .catch((e) => toast.error(`Error! ${e}`));
}

// Admin actions when maintenance mode is on
async function onReprojectCombats() {
    return await adminApi.reproject
        .combat()
        .then(() => {
            toast.success("Success!");
        })
        .catch((e) => toast.error(`Error! ${e}`));
}

async function onReadAndSaveCampaigns() {
    return await adminApi.readAndSave
        .campaigns()
        .then(() => {
            toast.success("Success!");
        })
        .catch((e) => toast.error(`Error! ${e}`));
}
</script>
