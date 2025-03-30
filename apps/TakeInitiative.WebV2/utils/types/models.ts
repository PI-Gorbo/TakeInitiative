import { z } from "zod";

// Campaign Member
const campaignMemberInfoValidator = z.object({
    memberId: z.string(),
    userId: z.string(),
    isDungeonMaster: z.boolean(),
});
export type CampaignMemberInfo = z.infer<typeof campaignMemberInfoValidator>;

// Campaign
export const HealthDisplayOptionsEnum = {
    RealValue: 0,
    HealthyBloodied: 1,
    Hidden: 2,
} as const;
export const HealthDisplayOptionValueKeyMap = {
    0: "RealValue",
    1: "HealthyBloodied",
    2: "Hidden",
};
export type HealthDisplayOptionKeys = keyof typeof HealthDisplayOptionsEnum;
export type HealthDisplayOptionValues =
    (typeof HealthDisplayOptionsEnum)[HealthDisplayOptionKeys];

export const ArmourClassDisplayOptionsEnum = {
    RealValue: 0,
    Hidden: 2,
} as const;
export const ArmourClassDisplayOptionValueKeyMap = {
    0: "RealValue",
    2: "Hidden",
};
export type ArmourClassDisplayOptionKeys =
    keyof typeof ArmourClassDisplayOptionsEnum;
export type ArmourClassDisplayOptionValues =
    (typeof ArmourClassDisplayOptionsEnum)[ArmourClassDisplayOptionKeys];

const campaignSettingsValidator = z.object({
    combatHealthDisplaySettings: z
        .object({
            dmCharacterDisplayMethod: z.nativeEnum(HealthDisplayOptionsEnum),
            otherPlayerCharacterDisplayMethod: z.nativeEnum(
                HealthDisplayOptionsEnum,
            ),
        })
        .required(),
    combatArmourClassDisplaySettings: z.object({
        dmCharacterDisplayMethod: z.nativeEnum(ArmourClassDisplayOptionsEnum),
        otherPlayerCharacterDisplayMethod: z.nativeEnum(
            ArmourClassDisplayOptionsEnum,
        ),
    }),
});
export type CampaignSettings = z.infer<typeof campaignSettingsValidator>;

export const campaignValidator = z
    .object({
        id: z.string(),
        ownerId: z.string(),
        campaignName: z.string(),
        campaignDescription: z.string(),
        plannedCombatIds: z.array(z.string()),
        campaignMemberInfo: z.array(campaignMemberInfoValidator),
        activeCombatId: z.string().nullable(),
        createdTimestamp: z.string(),
        campaignSettings: campaignSettingsValidator,
    })
    .required({
        id: true,
        ownerId: true,
        campaignName: true,
        campaignSettings: true,
    });
export type Campaign = z.infer<typeof campaignValidator>;

// Dice Roll
export const diceRollValidator = z
    .object({
        total: z.number().int(),
        roll: z.string(),
        evaluation: z.string(),
    })
    .required();
export type DiceRoll = z.infer<typeof diceRollValidator>;

// Unevaluated Character Initiative
export const unevaluatedCharacterInitiativeValidator = z
    .object({
        roll: z
            .string({ required_error: "An initiative value is required." })
            .min(1, "Please provide an initiative roll"),
    })
    .required();
export type UnevaluatedCharacterInitiative = z.infer<
    typeof unevaluatedCharacterInitiativeValidator
>;

// Character Initiative
export const characterInitiativeValidator = z.object({
    value: z.array(diceRollValidator),
});
export type CharacterInitiative = z.infer<typeof characterInitiativeValidator>;

// Unevaluated Character Health
export const unevaluatedCharacterHealthValidator = z.discriminatedUnion("!", [
    z
        .object({
            "!": z.literal("None"),
        })
        .required(),
    z
        .object({
            "!": z.literal("Roll"),
            rollString: z.string(),
        })
        .required(),
    z.object({
        "!": z.literal("Fixed"),
        currentHealth: z.number().int(),
        maxHealth: z.number().int(),
    }),
]);
export type UnevaluatedCharacterHealth = z.infer<
    typeof unevaluatedCharacterHealthValidator
>;

// Character Health
export const characterHealthValidator = z.discriminatedUnion("!", [
    z.object({
        "!": z.literal("None"),
    }),
    z.object({
        "!": z.literal("Fixed"),
        currentHealth: z.number().int(),
        maxHealth: z.number().int(),
        diceRoll: diceRollValidator.nullable(),
    }),
]);
export type CharacterHealth = z.infer<typeof characterHealthValidator>;

// Player Character
export const characterValidator = z
    .object({
        id: z.string(),
        name: z.string(),
        health: unevaluatedCharacterHealthValidator,
        initiative: unevaluatedCharacterInitiativeValidator,
        armourClass: z.number().nullable(),
    })
    .required();
export type ICharacter = z.infer<typeof characterValidator>;
export const playerCharacterValidator = characterValidator
    .extend({ playerId: z.string() })
    .required({ playerId: true });
export type PlayerCharacter = z.infer<typeof playerCharacterValidator>;

// Campaign Member
export enum ResourceVisibilityOptions {
    Private = 0,
    DMsOnly = 1,
    Public = 2,
}
export const resourceVisibilityOptionNameMap: Record<ResourceVisibilityOptions, string> = {
    [ResourceVisibilityOptions.DMsOnly]: 'You and DM',
    [ResourceVisibilityOptions.Public]: 'Everyone',
    [ResourceVisibilityOptions.Private]: 'Private'
} as const;
export const resourceVisibilityKeys = ["Private", "DMsOnly", "Public"] as const;
export const campaignMemberResourceValidator = z
    .object({
        name: z.string(),
        link: z.string(),
        visibility: z.nativeEnum(ResourceVisibilityOptions),
    })
    .required({ name: true, link: true });
export type CampaignMemberResource = z.infer<
    typeof campaignMemberResourceValidator
>;
export const campaignMemberValidator = z
    .object({
        id: z.string(),
        userId: z.string(),
        campaignId: z.string(),
        isDungeonMaster: z.boolean(),
        characters: z.array(playerCharacterValidator),
        resources: z.array(campaignMemberResourceValidator),
    })
    .required({
        id: true,
        userId: true,
        campaignId: true,
        isDungeonMaster: true,
    });
export type CampaignMember = z.infer<typeof campaignMemberValidator>;

// Planned Combat NPC
export const plannedCombatCharacterValidator = z
    .object({
        id: z.string(),
        name: z.string({ message: "Please provide a name" }),
        health: unevaluatedCharacterHealthValidator,
        initiative: unevaluatedCharacterInitiativeValidator,
        armourClass: z.number().nullable(),
        quantity: z.number(),
    })
    .required();
export type PlannedCombatCharacter = z.infer<
    typeof plannedCombatCharacterValidator
>;

// Planned Combat Stage
export const plannedCombatStageValidator = z
    .object({
        id: z.string().uuid(),
        name: z.string(),
        npcs: z.array(plannedCombatCharacterValidator),
    })
    .required();
export type PlannedCombatStage = z.infer<typeof plannedCombatStageValidator>;

// Planned Combat
export const plannedCombatValidator = z
    .object({
        id: z.string().uuid(),
        campaignId: z.string().uuid(),
        combatName: z.string(),
        stages: z.array(plannedCombatStageValidator),
    })
    .required({ id: true, campaignId: true, combatName: true });
export type PlannedCombat = z.infer<typeof plannedCombatValidator>;

// Combat
export enum CombatState {
    Open = 0,
    Started = 1,
    Paused = 2,
    Finished = 3,
}

export const playerDtoValidator = z
    .object({
        userId: z.string(),
    })
    .required({ userId: true });
export type PlayerDto = z.infer<typeof playerDtoValidator>;

// Conditions
export const conditionValidator = z.object({
    id: z.string(),
    name: z.string(),
    note: z.string(),
});
export type Condition = z.infer<typeof conditionValidator>;

// Character Origin Details
export const CharacterOriginOptions = {
    PlayerCharacter: 0,
    PlannedCharacter: 1,
    CustomCharacter: 2,
} as const;
export const characterOriginDetailsValidator = z
    .object({
        characterOrigin: z.nativeEnum(CharacterOriginOptions),
        id: z.string().nullable(),
    })
    .required();

export const initiativeCharacterValidator = z
    .object({
        id: z.string(),
        characterOriginDetails: characterOriginDetailsValidator,
        playerId: z.string(),
        hidden: z.boolean(),
        name: z.string(),
        health: characterHealthValidator,
        initiative: characterInitiativeValidator,
        armourClass: z.number().nullable(),
        copyNumber: z.number().nullable(),
        conditions: z.array(conditionValidator),
    })
    .required();

export type InitiativeCharacter = z.infer<typeof initiativeCharacterValidator>;

export const stagedCharacterValidator = characterValidator.extend({
    playerId: z.string().uuid(),
    characterOriginDetails: characterOriginDetailsValidator,
    hidden: z.boolean(),
    copyNumber: z.number().int().nullable(),
});
export type StagedCharacter = z.infer<typeof stagedCharacterValidator>;

/// COMBAT EVENTS
export const InitiativeRollDto = z
    .object({
        characterId: z.string().uuid(),
        roll: z.array(diceRollValidator),
        characterName: z.string(),
        rolledHealth: diceRollValidator.nullable(),
    })
    .required();

export const CombatStartedHistoryEvent = z.object({
    "!": z.literal("CombatStarted"),
});
export const CombatInitiativeRolledHistoryEvent = z.object({
    "!": z.literal("CombatInitiativeRolled"),
    rolls: z.array(InitiativeRollDto),
});
export type CombatInitiativeRolledHistoryEvent = z.infer<
    typeof CombatInitiativeRolledHistoryEvent
>;
export const CharacterHealthChangedHistoryEvent = z.object({
    "!": z.literal("CharacterHealthChanged"),
    characterId: z.string().uuid(),
    from: z.number().int(),
    to: z.number().int(),
});
export type CharacterHealthChangedHistoryEvent = z.infer<
    typeof CharacterHealthChangedHistoryEvent
>;
export const CombatFinishedHistoryEvent = z.object({
    "!": z.literal("CombatFinished"),
});
export const turnStartedHistoryEvent = z.object({
    "!": z.literal("TurnStarted"),
    characterId: z.string().uuid(),
});
export type TurnStartedHistoryEvent = z.infer<typeof turnStartedHistoryEvent>;
export const turnEndedHistoryEvent = z.object({
    "!": z.literal("TurnEnded"),
    characterId: z.string().uuid(),
});
export type TurnEndedHistoryEvent = z.infer<typeof turnEndedHistoryEvent>;
export const RoundEndedHistoryEvent = z.object({
    "!": z.literal("RoundEnded"),
});
export type RoundEndedHistoryEvent = z.infer<typeof RoundEndedHistoryEvent>;
export const CharacterRemovedHistoryEvent = z.object({
    "!": z.literal("CharacterRemoved"),
    characterId: z.string().uuid(),
});
export type CharacterRemovedHistoryEvent = z.infer<
    typeof CharacterRemovedHistoryEvent
>;
export const CombatInitiativeModifiedHistoryEvent = z.object({
    "!": z.literal("CombatInitiativeModified"),
    newInitiativeList: z.array(
        z
            .object({
                characterId: z.string().uuid(),
                roll: z.array(diceRollValidator),
                characterName: z.string(),
            })
            .required(),
    ),
});
export type CombatInitiativeModifiedHistoryEvent = z.infer<
    typeof CombatInitiativeModifiedHistoryEvent
>;

export const CharacterConditionAddedHistoryEvent = z.object({
    "!": z.literal("CharacterConditionAdded"),
    conditionId: z.string().uuid(),
    characterId: z.string().uuid(),
    name: z.string(),
});
export type CharacterConditionAddedHistoryEvent = z.infer<
    typeof CharacterConditionAddedHistoryEvent
>;

export const CharacterConditionRemovedHistoryEvent = z.object({
    "!": z.literal("CharacterConditionRemoved"),
    conditionId: z.string().uuid(),
    characterId: z.string().uuid(),
    name: z.string(),
});
export type CharacterConditionRemovedHistoryEvent = z.infer<
    typeof CharacterConditionRemovedHistoryEvent
>;

export const historyEventValidator = z.discriminatedUnion("!", [
    CombatStartedHistoryEvent,
    CombatInitiativeRolledHistoryEvent,
    CharacterHealthChangedHistoryEvent,
    CombatFinishedHistoryEvent,
    turnStartedHistoryEvent,
    turnEndedHistoryEvent,
    RoundEndedHistoryEvent,
    CharacterRemovedHistoryEvent,
    CharacterConditionAddedHistoryEvent,
    CharacterConditionRemovedHistoryEvent,
    CombatInitiativeModifiedHistoryEvent,
]);
export type HistoryEvent = z.infer<typeof historyEventValidator>;

export const historyEntryValidator = z
    .object({
        timestamp: z.string(),
        events: z.array(historyEventValidator),
        executor: z.string().uuid(),
    })
    .required({ timestamp: true, events: true, executor: true });
export type HistoryEntry = z.infer<typeof historyEntryValidator>;

/// COMBAT
export const combatValidator = z
    .object({
        id: z.string(),
        campaignId: z.string(),
        state: z.nativeEnum(CombatState),
        combatName: z.string(),
        dungeonMaster: z.string(),
        history: z.array(historyEntryValidator),
        currentPlayers: z.array(playerDtoValidator),
        plannedStages: z.array(plannedCombatStageValidator),
        initiativeList: z.array(initiativeCharacterValidator),
        stagedList: z.array(stagedCharacterValidator),
        initiativeIndex: z.number().nullable(),
        roundNumber: z.number().nullable(),
    })
    .required({
        id: true,
        campaignId: true,
        state: true,
        combatName: true,
        dungeonMaster: true,
        currentPlayers: true,
        plannedStages: true,
        initiativeList: true,
        stagedList: true,
    });
export type Combat = z.infer<typeof combatValidator>;
