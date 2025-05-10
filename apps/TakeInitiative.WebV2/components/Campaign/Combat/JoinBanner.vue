<template>
  <NuxtLink
      v-if="props.combatInfo"
      :to="{
            name: 'app-campaigns-campaignId-combats-combatId',
            params: {
                campaignId: props.campaignId,
                combatId: props.combatInfo.id,
            },
        }">
    <Button
        :class="`flex justify-between h-fit w-full bg-gold hover:bg-destructive text-gold-foreground hover:border-destructive ${styles.interactable} shadow-gold-shade border-gold-shade hover:border-destructive-shade hover:shadow-destructive-shade active:shadow-gold-shade active:hover:shadow-destructive-shade`"
        variant="outline">
      <header class="font-semibold">Join the combat!</header>
      <div class="flex gap-1">
        <FontAwesomeIcon :icon="faFistRaised"/>
        <p>
          {{ props.combatInfo.combatName }}
        </p>
      </div>
    </Button>
  </NuxtLink>
</template>
<script setup lang="ts">
import {faFistRaised} from "@fortawesome/free-solid-svg-icons";
import {FontAwesomeIcon} from "@fortawesome/vue-fontawesome";
import type {GetCampaignResponse} from "~/utils/api/campaign/getCampaignRequest";

const props = defineProps<{
  campaignId: string;
  combatInfo: GetCampaignResponse["currentCombatInfo"];
}>();
</script>
