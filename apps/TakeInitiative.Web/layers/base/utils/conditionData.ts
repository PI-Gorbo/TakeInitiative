export const Dnd5eConditionList = [
    "Blinded",
    "Charmed",
    "Deafened",
    "Frightened",
    "Grappled",
    "Incapacitated",
    "Invisible",
    "Paralyzed",
    "Petrified",
    "Poisoned",
    "Prone",
    "Restrained",
    "Stunned",
    "Unconscious",
    "Exhausted",
];
export type Dnd5eCondition = (typeof Dnd5eConditionList)[number];
export type IdentifiedConditionInfo = {
    icon: string;
};
// export const Dnd5eConditionInfoMap: Record<
//     Dnd5eCondition,
//     IdentifiedConditionInfo
// > = {
//     Blinded: {
//         icon: "eye-slash",
//     },
//     Charmed: {
//         icon: "heart",
//     },
//     Deafened: {
//         icon: "ear-deaf",
//     },
//     Frightened: {
//         icon: "face-surprise",
//     },
//     Grappled: {
//         icon: "hands",
//     },
//     Incapacitated: {
//         icon: "hands-bound",
//     },
//     Invisible: {},
//     Paralyzed: {},
//     Petrified: {},
//     Poisoned: {},
//     Prone: {},
//     Restrained: {
//         icon: "hands-bound",
//     },
//     Stunned: {},
//     Unconscious: {},
//     Exhausted: {},
// };
