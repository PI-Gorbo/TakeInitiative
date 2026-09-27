import { IMAGE_PARAM } from "~/utils/images";

// Whether the open viewer added its own history entry, so closing it goes back
// (the phone's back gesture and ✕ then do the same thing).
let pushed = false;
// Which viewer opened the image (16d): a gallery mounts its own viewer, with a swipe
// through the whole gallery, and the list's viewer on the same page stays shut. A
// deep link has no source, so the page's list viewer takes it.
const source = ref<string | undefined>();

/**
 * The full-screen image viewer's place in the URL (16c): `?image={imageId}`. Opening
 * pushes a history entry, so the back gesture closes the viewer; swiping replaces it.
 * `open(id, from)` names the viewer that answers (`ImageViewer`'s `source`).
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

    function open(id: string, from?: string) {
        pushed = true;
        source.value = from;
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
        if (!id) {
            pushed = false;
            source.value = undefined;
        }
    });

    return { imageId, source: readonly(source), open, show, close };
}
