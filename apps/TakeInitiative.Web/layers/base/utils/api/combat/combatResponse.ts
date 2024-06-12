import { combatValidator } from "../../types/models";
import * as yup from "yup";
export const combatResponseValidator = yup.object({
    combat: combatValidator,
});
