<template>
    <main class="flex w-full flex-col items-center gap-2 p-2">
        <section
            class="rounded-lg border border-take-navy-medium p-2 md:w-4/5 md:max-w-[1200px]"
            v-if="status == 'success'"
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
        <section v-else>
            {{ status }}
        </section>
    </main>
</template>
<script setup lang="ts">
import { toTypedSchema } from "@vee-validate/yup";
import { useForm } from "vee-validate";
import {
    MaintenanceConfigValidator,
    type MaintenanceConfig,
} from "base/utils/api/admin/getMaintainenceRequest";

const { data, refresh, status } = await useAsyncData(
    "maintenanceQuery",
    async () => {
        const api = useApi();
        const maintenanceData = await api.admin.getMaintenance();
        return {
            maintenanceData,
        };
    },
);

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

    return await useAdminApi()
        .putMaintenance({
            inMaintenanceMode: true,
            reason: reason.value!,
        })
        .then((inputData) => {
            if (data.value) return (data.value.maintenanceData = inputData);
        });
}
async function onDisableMaintenanceMode() {
    return await useAdminApi()
        .putMaintenance({
            inMaintenanceMode: false,
            reason: null,
        })
        .then((inputData) => {
            if (data.value) return (data.value.maintenanceData = inputData);
        });
}
</script>
