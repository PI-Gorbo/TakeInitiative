import { useMediaQuery } from "@vueuse/core"

export const useScreenSize = () => {
    return {
        isLargeScreen: useMediaQuery("(min-width: 1024px)")
    }
}