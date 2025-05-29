import { toast } from "vue-sonner";
import { useEndTurnMutation, useFinishCombatMutation, useStartCombatMutation } from "~/utils/queries/combats";
import type { RefOrGetter } from "~/utils/queries/utils";

export const useCombatControls = (combatId: RefOrGetter<string>) => {

    const getToastPosition = () => useDevice().isMobile ? 'top-right' : 'bottom-right'


    const endTurnMutation = useEndTurnMutation();
    const endTurn = async () => {
        await endTurnMutation
            .mutateAsync({
                combatId: toValue(combatId),
            })
            .then(() => toast.success("Ended Turn!", {
                position: getToastPosition()
            }))
            .catch(() => toast.error("Failed to end turn!"));
    };

    const finishCombatMutation = useFinishCombatMutation();
    const finishCombat = async () => {
        await finishCombatMutation
            .mutateAsync({
                combatId: toValue(combatId),
            })
            .then(() => toast.success("Combat Finished!"))
            .catch(() =>
                toast.error(
                    "Something went wrong while trying to finish the combat"
                )
            );
    };

    const startCombatMutation = useStartCombatMutation();
    const startCombat = async () => {
        await startCombatMutation
            .mutateAsync({
                combatId: toValue(combatId),
            })
            .then(() => toast.success("Combat Started!"))
            .catch(() =>
                toast.error(
                    "Something went wrong while trying to start the combat"
                )
            );
    };

    return {
        endTurn,
        startCombat,
        finishCombat,
    }
}