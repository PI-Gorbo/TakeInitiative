import { combatValidator } from "base/utils/types/models";
import * as yup from "yup";
export const combatResponseValidator = yup.object({
    combat: combatValidator,
});
