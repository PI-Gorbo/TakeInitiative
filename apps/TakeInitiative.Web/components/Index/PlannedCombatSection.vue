<template>
    <!-- <main class="w-full overflow-auto px-4 py-2">
        <div class="mb-2 flex select-none flex-row text-take-yellow">
            <div @onclick="@(() => SelectFight(null))">
                <IconButton
                    IconName="fa-arrow-left"
                    Colour="TakeInitiativeColour.TakeNavyLight"
                >
                    <label class="cursor-pointer select-none"> Back </label>
                </IconButton>
            </div>
            <div class="flex flex-1 justify-center">
                <label>@SelectedCombat.CombatName</label>
            </div>
            <div @onclick="() => DeleteCombat(SelectedCombat)">
                <IconButton
                    IconName="fa-trash"
                    Colour="TakeInitiativeColour.TakeNavyLight"
                />
            </div>
        </div>

        <div class="flex flex-col gap-4">
            @foreach (var stage in SelectedCombat.Stages) {
            <div class="flex w-full justify-center">
                <div
                    class="w-4/5 rounded-xl border-2 border-take-navy-light p-2"
                >
                    <div class="mb-2 flex w-full flex-row">
                        <div class="flex-1 text-take-yellow">@stage.Name</div>
                        
                        <div @onclick="() => ShowCreateNpcModal(stage)">
                            <IconButton
                                IconName="fa-plus"
                                Colour="TakeInitiativeColour.TakeNavyMedium"
                            />
                        </div>
                        <div
                            @onclick="() => DeleteStage(SelectedCombat, stage)"
                        >
                            <IconButton
                                IconName="fa-trash"
                                Colour="TakeInitiativeColour.TakeNavyMedium"
                                HighlighTextColour="TakeInitiativeColour.TakeRed"
                            />
                        </div>
                    </div>
                    <div class="overflow flex gap-2 overflow-x-auto pb-4">
                        @foreach (var npc in stage.NPCs) {
                        <section
                            class="min-w-fit cursor-pointer rounded-xl border border-take-navy-light p-2 hover:border-take-yellow"
                            @onclick="() => EditNpc(SelectedCombat, stage, npc)"
                        >
                            <label class="cursor-pointer"
                                >@npc.Name (@npc.Quantity)</label
                            >
                            <div class="flex justify-between gap-2">
                                @if (npc.CharacterHealth != null) {
                                <div class="min-w-fit">
                                    <FontAwesomeIcon Icon="fa-droplet" />
                                    @npc.CharacterHealth.CurrentHealth/@npc.CharacterHealth.MaxHealth
                                </div>
                                } @if (npc.ArmorClass != null) {
                                <div class="min-w-fit">
                                    <FontAwesomeIcon Icon="fa-shield-halved" />
                                    @npc.ArmorClass
                                </div>
                                }
                                <div class="min-w-fit">
                                    <FontAwesomeIcon Icon="fa-shoe-prints" />
                                    @npc.CharacterInitiative.ToString()
                                </div>
                            </div>
                        </section>
                        }
                        
                        <section
                            class="min-w-fit cursor-pointer rounded-xl border-2 border-dashed border-take-navy-light p-2 hover:border-take-yellow"
                            @onclick="() => ShowCreateNpcModal(stage)"
                        >
                            <div class="p-2">
                                <FontAwesomeIcon Icon="fa-plus" />
                            </div>
                        </section>
                    </div>
                </div>
            </div>
            }
        </div>

        
        <div class="flex justify-center">
            <div
                class="w-fullrounded-xl min-h-2em my-3 flex cursor-pointer justify-center border-2 border-dashed border-take-navy-light hover:border-take-yellow"
                @onclick="ShowCreateStageModal"
            >
                <div class="p-2">
                    <FontAwesomeIcon Icon="fa-plus" />
                </div>
            </div>
        </div>
    </main> -->
</template>
<script setup lang="ts"></script>
