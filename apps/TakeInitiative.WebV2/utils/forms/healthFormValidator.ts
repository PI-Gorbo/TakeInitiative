import { Parser } from "expr-eval";
import { z } from "zod";

const healthExpressionParser = new Parser({
    operators: {
        // Only enable add, subtract, multiple and divide
        add: true,
        subtract: true,
        multiply: true,
        divide: true,
        concatenate: false,
        conditional: false,
        factorial: false,
        power: false,
        remainder: false,

        // Disable and, or, not, <, ==, !=, etc.
        logical: false,
        comparison: false,

        // Disable 'in' and = operators
        in: false,
        assignment: false,
    },
});
const tryParseNumber = (
    num: string | null
):
    | { isSuccess: true; value: number | null }
    | { isSuccess: false; error: string } => {
    if (num == null) return { isSuccess: true, value: null };
    if (num == "")
        return { isSuccess: false, error: "Please provide a value" };

    try {
        return {
            isSuccess: true,
            value: healthExpressionParser.evaluate(num),
        };
    } catch (e) {
        return {
            isSuccess: false,
            error: `failed to evaluate expression '${num}'`,
        };
    }
};

type Result = {
    success: false
    message: string
} | {
    success: true,
    value: number | null
}
function tryParseToNumberIfString(value: string | number | null): Result {
    if (typeof value === 'string') {

        const parsedCurrentHealth = tryParseNumber(value);
        if (!parsedCurrentHealth.isSuccess) {
            return {
                success: false as const,
                message: parsedCurrentHealth.error
            }
        }

        return {
            success: true as const,
            value: parsedCurrentHealth.value
        }
    }
    return {
        success: true as const,
        value: value as number | null
    }

}


export type FormHealthInput = z.infer<typeof healthInputValidator>
export const healthInputValidator =
    z.discriminatedUnion("!", [
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
            currentHealth: z.union([z.string(), z.number()]),
            maxHealth: z.union([z.string(), z.number()]),
        })
    ]);

export function evaluateHealthInput(healthInput: FormHealthInput): FormHealthInput {
    if (healthInput["!"] === 'None' || healthInput['!'] === 'Roll') {
        return healthInput
    }

    const parsedCurrentHealth = tryParseToNumberIfString(
        healthInput.currentHealth
    );

    if (!parsedCurrentHealth.success) {
        return healthInput;
    }

    const parsedMaxHealth = tryParseToNumberIfString(healthInput.maxHealth);
    if (!parsedMaxHealth.success) {
        return healthInput;
    }

    return {
        "!": 'Fixed',
        currentHealth: parsedCurrentHealth.value ?? 0,
        maxHealth: parsedMaxHealth.value ?? 0,
    };
}

export type MappedCharacterHealth = z.infer<typeof mappedHealthInputValidator>
export const mappedHealthInputValidator = healthInputValidator.transform((healthValues, ctx) => {

    if (healthValues["!"] === 'None' || healthValues['!'] === 'Roll') {
        return healthValues
    }

    const parsedCurrentHealth = tryParseToNumberIfString(
        healthValues.currentHealth
    );
    if (!parsedCurrentHealth.success) {
        ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: parsedCurrentHealth.message + " for current health",
        });
        return z.NEVER;
    }

    const parsedMaxHealth = tryParseToNumberIfString(healthValues.maxHealth);
    if (!parsedMaxHealth.success) {
        ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: parsedMaxHealth.message + " for max health",
        });
        return z.NEVER;
    }

    return {
        "!": 'Fixed',
        currentHealth: parsedCurrentHealth.value ?? 0,
        maxHealth: parsedMaxHealth.value ?? 0,
    } satisfies FormHealthInput;
})
