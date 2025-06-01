<template>
    <main class="flex h-full flex-col gap-4 p-2">
        <Card class="border-primary/50">
            <CardHeader>
                <CardTitle> Edit Campaign Settings </CardTitle>
            </CardHeader>
            <CardContent>
                <AutoForm
                    :schema="formSchema"
                    :form="settingsForm"
                    :fieldConfig="{
                        combatHealthDisplaySettings: {
                            dmCharacterDisplayMethod: {
                                label: 'How should the players see the health of the DM\'s characters?',
                            },
                            otherPlayerCharacterDisplayMethod: {
                                label: 'How should the players see the health of other players?',
                            },
                        },
                        combatArmourClassDisplaySettings: {
                            dmCharacterDisplayMethod: {
                                label: 'How should the players see the AC of the DM\'s characters?',
                            },
                            otherPlayerCharacterDisplayMethod: {
                                label: 'How should the players see the AC of other players?',
                            },
                        },
                    }"
                    :onSubmit="() => submit()">
                    <div class="flex justify-end">
                        <AsyncButton
                            :disabled="!settingsForm.meta.value.dirty"
                            type="submit"
                            label="Save"
                            loadingLabel="Saving..."
                            :icon="faSave"
                            :isLoading="settingsForm.isSubmitting.value" />
                    </div>
                </AutoForm>
            </CardContent>
        </Card>

        <Card class="border-destructive/50">
            <CardHeader>
                <CardTitle>Danger Zone</CardTitle>
            </CardHeader>

            <CardContent>
                <AsyncButton
                    label="Delete Campaign"
                    loadingLabel="Deleting..."
                    variant="destructive"
                    :icon="faTrash"
                    :click="
                        () =>
                            userStore.deleteCampaign({
                                campaignId: route.params.campaignId,
                            })
                    " />
            </CardContent>
        </Card>
    </main>
</template>
<script setup lang="ts">
    import { faSave, faTrash } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useQuery } from "@tanstack/vue-query";
    import { helpers } from "@typed-router";
    import { toTypedSchema } from "@vee-validate/zod";
    import { useForm } from "vee-validate";
    import { toast } from "vue-sonner";
    import { z } from "zod";
    import type { UpdateCampaignDetailsRequest } from "~/utils/api/campaign/updateCampaignDetailsRequest";
    import {
        getCampaignQuery,
        updateCampaignDetailsMutation,
    } from "~/utils/queries/campaign";
    import {
        HealthDisplayOptionsEnum,
        HealthDisplayOptionValueKeyMap,
        type HealthDisplayOptionValues,
        type HealthDisplayOptionKeys,
        type ArmourClassDisplayOptionValues,
        ArmourClassDisplayOptionValueKeyMap,
        type ArmourClassDisplayOptionKeys,
        ArmourClassDisplayOptionsEnum,
    } from "~/utils/types/models";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
        middleware: [
            async function MustBeDungeonMaster(to, from) {
                const campaignQuery = useQuery(
                    getCampaignQuery(() => to.params.campaignId as string)
                );
                const campaignData = await campaignQuery.suspense();
                if (!campaignData?.data?.userCampaignMember.isDungeonMaster) {
                    return navigateTo(
                        helpers.route({
                            name: "app-campaigns-campaignId",
                            params: {
                                campaignId: to.params.campaignId as string,
                            },
                        })
                    );
                }
            },
        ],
    });

    const route = useRoute("app-campaigns-campaignId-settings");
    const campaignQuery = useQuery(
        getCampaignQuery(() => route.params.campaignId as string)
    );

    const userStore = useUserStore();
    const campaignDto = computed(() => {
        return campaignQuery.data?.value;
    });
    const healthEnum = z.enum(["RealValue", "HealthyBloodied", "Hidden"]);
    type HealthEnumType = z.infer<typeof healthEnum>;
    function healthLabelToEnumValue(
        healthEnumlabel: HealthEnumType
    ): HealthDisplayOptionValues {
        return HealthDisplayOptionsEnum[healthEnumlabel];
    }
    function healthEnumValueToLabel(
        enumValue: HealthDisplayOptionValues
    ): HealthEnumType {
        return HealthDisplayOptionValueKeyMap[enumValue] as HealthEnumType;
    }

    const acEnum = z.enum(["RealValue", "Hidden"]);
    type AcEnumType = z.infer<typeof acEnum>;
    function acLabelToEnumValue(
        acEnumLabel: AcEnumType
    ): ArmourClassDisplayOptionValues {
        return ArmourClassDisplayOptionsEnum[acEnumLabel];
    }
    function acEnumValueToLabel(
        enumValue: ArmourClassDisplayOptionValues
    ): AcEnumType {
        return ArmourClassDisplayOptionValueKeyMap[enumValue] as AcEnumType;
    }
    const formSchema = z.object({
        campaignName: z.string().min(1, "Campaign name is required"),
        combatHealthDisplaySettings: z.object({
            dmCharacterDisplayMethod: healthEnum,
            otherPlayerCharacterDisplayMethod: healthEnum,
        }),
        combatArmourClassDisplaySettings: z.object({
            dmCharacterDisplayMethod: acEnum,
            otherPlayerCharacterDisplayMethod: acEnum,
        }),
    });
    function generateInitalValues() {
        return {
            campaignName: campaignDto.value?.campaign.campaignName ?? "",
            combatHealthDisplaySettings: {
                dmCharacterDisplayMethod: healthEnumValueToLabel(
                    campaignDto.value?.campaign?.campaignSettings
                        .combatHealthDisplaySettings.dmCharacterDisplayMethod ??
                        HealthDisplayOptionsEnum.HealthyBloodied
                ),
                otherPlayerCharacterDisplayMethod: healthEnumValueToLabel(
                    campaignDto.value?.campaign?.campaignSettings
                        .combatHealthDisplaySettings
                        .otherPlayerCharacterDisplayMethod ??
                        HealthDisplayOptionsEnum.HealthyBloodied
                ),
            },
            combatArmourClassDisplaySettings: {
                dmCharacterDisplayMethod: acEnumValueToLabel(
                    campaignDto.value?.campaign?.campaignSettings
                        .combatArmourClassDisplaySettings
                        .dmCharacterDisplayMethod ??
                        ArmourClassDisplayOptionsEnum.RealValue
                ),
                otherPlayerCharacterDisplayMethod: acEnumValueToLabel(
                    campaignDto.value?.campaign?.campaignSettings
                        .combatArmourClassDisplaySettings
                        .otherPlayerCharacterDisplayMethod ??
                        ArmourClassDisplayOptionsEnum.RealValue
                ),
            },
        };
    }
    const settingsForm = useForm({
        validationSchema: toTypedSchema(formSchema),
        initialValues: generateInitalValues(),
        keepValuesOnUnmount: true,
    });

    function undefinedIfValuesAreEqual<T>(a: T, b: T): T | undefined {
        return a === b ? undefined : a;
    }
    const updateCampaignDetails = updateCampaignDetailsMutation();
    const submit = settingsForm.handleSubmit(async (formValues) =>
        updateCampaignDetails
            .mutateAsync({
                campaignId: route.params.campaignId as string,
                campaignName: undefinedIfValuesAreEqual(
                    formValues.campaignName,
                    campaignDto.value?.campaign.campaignName
                ),
                campaignSettings: {
                    combatHealthDisplaySettings: {
                        dmCharacterDisplayMethod: healthLabelToEnumValue(
                            formValues.combatHealthDisplaySettings
                                .dmCharacterDisplayMethod
                        ),
                        otherPlayerCharacterDisplayMethod:
                            healthLabelToEnumValue(
                                formValues.combatHealthDisplaySettings
                                    .otherPlayerCharacterDisplayMethod
                            ),
                    },
                    combatArmourClassDisplaySettings: {
                        dmCharacterDisplayMethod: acLabelToEnumValue(
                            formValues.combatArmourClassDisplaySettings
                                .dmCharacterDisplayMethod
                        ),
                        otherPlayerCharacterDisplayMethod: acLabelToEnumValue(
                            formValues.combatArmourClassDisplaySettings
                                .otherPlayerCharacterDisplayMethod
                        ),
                    },
                },
            })
            .then(() => {
                toast.success("Updated Campaign Settings");
                settingsForm.resetForm({
                    values: generateInitalValues(),
                });
            })
    );
</script>
