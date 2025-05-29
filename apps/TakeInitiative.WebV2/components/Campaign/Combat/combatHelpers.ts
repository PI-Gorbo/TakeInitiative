
import type {
    InitiativeCharacter,
    StagedCharacter,
} from "~/utils/types/models";

export type CharacterDto = StagedPlayerDto | InitiativePlayerDto;

export function isInitiativeCharacter(
    character: InitiativeCharacter | StagedCharacter
): character is InitiativeCharacter {
    return (character as InitiativeCharacter).initiative.value !== undefined;
}

export function isStagedCharacter(
    character: InitiativeCharacter | StagedCharacter
): character is StagedCharacter {
    return (character as StagedCharacter).initiative.roll !== undefined;
}
