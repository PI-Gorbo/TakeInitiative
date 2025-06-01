import {
    faBed,
    faBolt,
    faCircleExclamation,
    faEarDeaf,
    faEyeLowVision,
    faFaceDizzy,
    faFlask,
    faGhost,
    faHand,
    faHeart,
    faMap,
    faMask,
    faPerson,
    faSurprise,
    faUserSlash,
    type IconDefinition as SolidIconDefinition,
} from "@fortawesome/free-solid-svg-icons";
import type { TakeInitColour } from "./types/HelperTypes";
import {
    faOdysee,
    type IconDefinition as BrandIconDefinition,
} from "@fortawesome/free-brands-svg-icons";
import { faHandshakeSimple } from "@fortawesome/free-solid-svg-icons/faHandshakeSimple";

type Icon = SolidIconDefinition | BrandIconDefinition;
export type DefaultCondition = {
    name: string;
    icon: Icon;
};

const AvailableConditionBackgroundColours: TakeInitColour[] = [
    "take-teal",
    "take-purple",
    "take-purple-light",
    "take-red",
    "take-yellow-dark",
    "take-cream",
];

export const DefaultConditions: DefaultCondition[] = [
    { name: "Blinded", icon: faEyeLowVision },
    { name: "Charmed", icon: faHeart },
    { name: "Deafened", icon: faEarDeaf },
    { name: "Exhaustion", icon: faBed },
    { name: "Frightened", icon: faGhost },
    { name: "Grappled", icon: faHandshakeSimple },
    { name: "Incapacitated", icon: faFaceDizzy },
    { name: "Invisible", icon: faMask },
    { name: "Paralyzed", icon: faPerson },
    { name: "Petrified", icon: faSurprise },
    { name: "Poisoned", icon: faFlask },
    { name: "Prone", icon: faOdysee },
    { name: "Restrained", icon: faHand },
    { name: "Stunned", icon: faBolt },
    { name: "Unconscious", icon: faUserSlash },
];

export function getConditionBackgroundColour(
    conditionName: string,
): TakeInitColour {
    // Convert name to all caps.
    const upperCase = conditionName.toUpperCase();
    let sum = 0;
    for (let index = 0; index < upperCase.length; index++) {
        sum += upperCase.charCodeAt(index);
    }
    // Wrap the sum between 0 and the available background colour options.
    const wrappedValue = sum % AvailableConditionBackgroundColours.length;
    return AvailableConditionBackgroundColours[wrappedValue];
}

export function getConditionIcon(conditionName: string): Icon {
    const defaultCondition = DefaultConditions.find(
        (x) => x.name == conditionName,
    );
    if (defaultCondition != null) {
        return defaultCondition.icon;
    }

    return faCircleExclamation;
}
