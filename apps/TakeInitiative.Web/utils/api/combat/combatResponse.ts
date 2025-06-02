import { combatValidator } from "../../types/models";
import { z } from "zod";
export const combatResponseValidator = z
    .object({
        combat: combatValidator,
    })
    .required();
