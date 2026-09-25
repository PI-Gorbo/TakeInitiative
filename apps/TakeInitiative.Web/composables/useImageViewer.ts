import { IMAGE_PARAM } from "~/utils/images";

// Whether the open viewer added its own history entry, so closing it goes back
// (the phone's back gesture and ✕ then do the same thing).
let pushed = false;

/**
 * The full-screen image viewer's place in the URL (16c): `?image={imageId}`. Opening
 * pushes a history entry, so the back gesture closes the viewer; swiping replaces it.
 */
export function useImageViewer() {
    const route = useRoute();
    const router = useRouter();

    const imageId = computed(() => {
        const value = route.query[IMAGE_PARAM];
        return typeof value === "string" && value ? value : undefined;
    });

    const withImage = (id: string | undefined) => {
        const { [IMAGE_PARAM]: _, ...query } = route.query;
        return id ? { ...query, [IMAGE_PARAM]: id } : query;
    };

    function open(id: string) {
        pushed = true;
        void router.push({ query: withImage(id) });
    }

    function show(id: string) {
        void router.replace({ query: withImage(id) });
    }

    function close() {
        if (!imageId.value) return;
        if (pushed) {
            pushed = false;
            router.back();
        } else {
            void router.replace({ query: withImage(undefined) });
        }
    }

    // Closed some other way (the back gesture): the next close must not go back again.
    watch(imageId, (id) => {
        if (!id) pushed = false;
    });

    return { imageId, open, show, close };
}
