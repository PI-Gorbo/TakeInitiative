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
        campaignResources: z.string(),
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

// Character Initiative
export enum InitiativeStrategy {
    Fixed = 0,
    Roll = 1,
}
export const characterInitiativeValidator = z
    .object({
        strategy: z.nativeEnum(InitiativeStrategy),
        value: z.string({ message: "An initiative value is required." }),
    })
    .required({ value: true });
export type CharacterInitiative = z.infer<typeof characterInitiativeValidator>;

// Character Health
export const characterHealthValidator = z
    .object({
        hasHealth: z.boolean(),
        maxHealth: z
            .number({ message: "Max health be a valid number" })
            .int("Max health be a integer (No decimal point)")
            .nullable(),
        currentHealth: z
            .number()
            .int("Current health be a integer (No decimal point)")
            .nullable(),
    })
    .required({ hasHealth: true })
    .refine(
        (characterHealth) => {
            if (!characterHealth.hasHealth) {
                return true;
            }

            return (
                characterHealth.maxHealth != null &&
                characterHealth.currentHealth != null
            );
        },
        {
            message:
                "Either set current and max health to empty with the reset button, or provide a value for both",
        },
    );
export type CharacterHealth = z.infer<typeof characterHealthValidator>;

// Player Character
export const characterValidator = z
    .object({
        id: z.string(),
        name: z.string(),
        health: characterHealthValidator.nullable(),
        initiative: characterInitiativeValidator,
        armourClass: z.number().nullable(),
    })
    .required({ id: true, name: true });
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
        health: characterHealthValidator,
        armourClass: z.number(),
        initiative: characterInitiativeValidator,
        quantity: z.number(),
    })
    .required({ name: true });
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
    .required({ id: true, name: true });
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

export const combatTimingRecordValidator = z.object({
    startTime: z.string(),
    endTime: z.string().nullable(),
});
export type CombatTiming = z.infer<typeof combatTimingRecordValidator>;
export const playerDtoValidator = z
    .object({
        userId: z.string(),
    })
    .required({ userId: true });
export type PlayerDto = z.infer<typeof playerDtoValidator>;

export const CharacterOriginOptions = {
    PlayerCharacter: 0,
    PlannedCharacter: 1,
    CustomCharacter: 2,
} as const;
export const combatCharacterValidator = z
    .object({
        id: z.string(),
        characterOriginDetails: z.object({
            characterOrigin: z.nativeEnum(CharacterOriginOptions),
            id: z.string().nullable(),
        }),
        playerId: z.string(),
        initiativeValue: z.array(z.number()).nullable(),
        hidden: z.boolean(),
        name: z.string(),
        health: characterHealthValidator.nullable(),
        initiative: characterInitiativeValidator,
        armourClass: z.number().nullable(),
        copyNumber: z.number().nullable(),
    })
    .required({ id: true, playerId: true, hidden: true, name: true });
export type CombatCharacter = z.infer<typeof combatCharacterValidator>;

export const CombatStartedHistoryEvent = z.object({});
export const CombatInitiativeRolledHistoryEvent = z.object({
    rolls: z.array(
        z.object({
            id: z.string().uuid(),
            rolls: z.array(z.number().int()),
        }),
    ),
});
export const CharacterHealthChangedHistoryEvent = z.object({
    characterId: z.string().uuid(),
    from: z.number().int(),
    to: z.number().int(),
});
export const CombatFinishedHistoryEvent = z.object({});
export const TurnStartedHistoryEvent = z.object({
    characterId: z.string().uuid(),
});
export const TurnEndedHistoryEvent = z.object({
    characterId: z.string().uuid(),
});
export const RoundEndedHistoryEvent = z.object({});
export const PlayerCharacterJoinedHistoryEvent = z.object({
    characterId: z.string().uuid(),
});
export const PlannedCharactersAddedHistoryEvent = z.object({});
export const CharacterRemovedHistoryEvent = z.object({
    characterId: z.string().uuid(),
});

export const historyEventValidator = z.discriminatedUnion("!", [
    CombatStartedHistoryEvent.extend({
        "!": z.literal("CombatStarted"),
    }),
    CombatInitiativeRolledHistoryEvent.extend({
        "!": z.literal("CombatInitiativeRolled"),
    }),
    CharacterHealthChangedHistoryEvent.extend({
        "!": z.literal("CharacterHealthChanged"),
    }),
    CombatFinishedHistoryEvent.extend({
        "!": z.literal("CombatFinished"),
    }),
    TurnStartedHistoryEvent.extend({
        "!": z.literal("TurnStarted"),
    }),
    TurnEndedHistoryEvent.extend({
        "!": z.literal("TurnEnded"),
    }),
    RoundEndedHistoryEvent.extend({
        "!": z.literal("RoundEnded"),
    }),
    PlayerCharacterJoinedHistoryEvent.extend({
        "!": z.literal("PlayerCharacterJoined"),
    }),
    PlannedCharactersAddedHistoryEvent.extend({
        "!": z.literal("PlannedCharactersAdded"),
    }),
    CharacterRemovedHistoryEvent.extend({
        "!": z.literal("CharacterRemoved"),
    }),
]);
export type HistoryEvent = z.infer<typeof historyEntryValidator>;

export const historyEntryValidator = z
    .object({
        timestamp: z.string(),
        events: z.array(historyEventValidator),
        executor: z.string().uuid(),
    })
    .required({ timestamp: true, events: true, executor: true });
export type HistoryEntry = z.infer<typeof historyEntryValidator>;

export const combatValidator = z
    .object({
        id: z.string(),
        campaignId: z.string(),
        state: z.nativeEnum(CombatState),
        combatName: z.string(),
        dungeonMaster: z.string(),
        combatLogs: z.array(z.string()),
        currentPlayers: z.array(playerDtoValidator),
        plannedStages: z.array(plannedCombatStageValidator),
        initiativeList: z.array(combatCharacterValidator),
        stagedList: z.array(combatCharacterValidator),
        initiativeIndex: z.number(),
        roundNumber: z.string().nullable(),
    })
    .required({
        id: true,
        campaignId: true,
        state: true,
        combatName: true,
        dungeonMaster: true,
        combatLogs: true,
        currentPlayers: true,
        plannedStages: true,
        initiativeList: true,
        stagedList: true,
    });
export type Combat = z.infer<typeof combatValidator>;
