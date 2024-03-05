import { combatValidator } from "~/utils/types/models";
import * as yup from "yup";
export const combatResponseValidator = yup.object({
    combat: combatValidator,
});