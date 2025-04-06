<template>
    <CampaignCharacterHealthDisplay :health="character.health" unstyled />
    <CampaignCharacterArmourClassDisplay :armourClass="character.armourClass" />
    <CampaignCharacterInitiativeDisplay
        :initiative="character.initiative.roll" />
</template>
