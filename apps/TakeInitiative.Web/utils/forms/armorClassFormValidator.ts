import { z } from "zod";

export const armorClassFormValidator =
    z.union([z.string(), z.number()])
        .nullable()
        .transform(
            (v, ctx) => {
                if (typeof v === 'number' || v == null) return v;
                if (v == '') return null;

                let n = Number(v);
                const isValidNunber = !isNaN(n) && v?.length > 0;
                if (!isValidNunber) {
                    ctx.addIssue({ message: "Invalid number", code: z.ZodIssueCode.custom })
                    return z.NEVER;
                }

                return n;
            },
        )