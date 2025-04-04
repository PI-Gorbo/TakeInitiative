import type { ShallowRef } from "vue";

export type RefOrGetter<T> = ShallowRef<T> | WritableComputedRef<T> | Ref<T> | ComputedRef<T> | (() => T)