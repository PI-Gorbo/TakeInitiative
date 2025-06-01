import type { ShallowRef } from "vue";

export type RefOrGetter<T> = ShallowRef<T> | WritableComputedRef<T> | Ref<T> | ComputedRef<T> | (() => T)

export const minutes = (num: number) => num * 60 * 1000;
export const seconds = (num: number) => num * 1000;