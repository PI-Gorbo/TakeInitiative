<template>
    <Card class="flex flex-col gap-3 p-3">
        <!-- "Suggestions on this device" (23b): a per-device setting, kept in localStorage. -->
        <div class="flex flex-col gap-2">
            <h3 class="font-medium">✨ Suggestions on this device</h3>
            <p class="text-sm text-muted-foreground">
                Suggestions come from a small model that runs inside your browser, on this device. What you write in
                your campaign is <b class="font-medium text-foreground">never sent to an outside AI service</b> — not to
                OpenAI, not to Anthropic, not to anyone else — and it never will be. Nothing is linked until you accept
                it.
            </p>
            <p class="text-sm text-muted-foreground">
                Two things do leave this device: the model's own files, downloaded once from
                {{ extractor.source.value.host }}, and the names the model picks out, which go to this campaign's own
                server to ask whether they already have a wiki entry. Your notes themselves stay here.
            </p>
        </div>

        <RadioGroup
            :modelValue="extractor.setting.value"
            class="gap-1"
            @update:modelValue="(v) => extractor.setSetting(v as SuggestionsSetting)">
            <Label
                v-for="option in OPTIONS"
                :key="option.value"
                :for="`suggestions-${option.value}`"
                class="flex min-h-11 cursor-pointer items-start gap-3 rounded-md px-2 py-2 font-normal hover:bg-accent md:min-h-0">
                <RadioGroupItem
                    :id="`suggestions-${option.value}`"
                    :value="option.value"
                    class="mt-0.5" />
                <span class="flex flex-col">
                    <span class="font-medium">{{ option.label }}</span>
                    <span class="text-xs text-muted-foreground">{{ option.hint }}</span>
                </span>
            </Label>
        </RadioGroup>

        <dl class="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
            <dt class="text-muted-foreground">Model</dt>
            <dd class="min-w-0 break-words">{{ config.name }} <span class="text-muted-foreground">({{ config.id }})</span></dd>
            <dt class="text-muted-foreground">Licence</dt>
            <dd class="min-w-0 break-words">
                {{ config.licence }} ·
                <a
                    :href="config.upstream"
                    target="_blank"
                    rel="noopener"
                    class="underline underline-offset-2">{{ config.attribution }}</a>
            </dd>
            <dt class="text-muted-foreground">Files from</dt>
            <dd class="min-w-0 break-words">{{ extractor.source.value.host }}</dd>
            <dt class="text-muted-foreground">On this device</dt>
            <dd>{{ extractor.cached.value ? formatMegabytes(extractor.cached.value.bytes) : "Not downloaded" }}</dd>
        </dl>

        <div
            v-if="busy"
            class="flex flex-col gap-2"
            role="status">
            <p class="text-sm">
                {{ state === "downloading" ? "Downloading the suggestion model" : "Loading the suggestion model" }} ·
                {{ Math.round(progress.loaded / 1e6) }} of {{ formatMegabytes(progress.total) }}
            </p>
            <div class="h-1.5 overflow-hidden rounded bg-muted">
                <div
                    class="h-full bg-primary transition-[width]"
                    :style="{ width: `${progress.total ? (100 * progress.loaded) / progress.total : 0}%` }" />
            </div>
        </div>
        <p
            v-if="state === 'error'"
            class="text-sm text-destructive"
            role="alert">
            {{ extractor.error.value }}
        </p>

        <div class="flex flex-wrap gap-2">
            <Button
                v-if="state !== 'off' && state !== 'ready' && !busy"
                variant="outline"
                class="min-h-11 md:min-h-9"
                @click="download">
                {{ state === "error" ? "Retry" : extractor.cached.value ? "Load now" : `Download (${formatMegabytes(extractor.source.value.bytes)})` }}
            </Button>
            <Button
                v-if="busy"
                variant="outline"
                class="min-h-11 md:min-h-9"
                @click="extractor.cancel()">
                Cancel
            </Button>
            <Button
                v-if="state === 'ready'"
                variant="outline"
                class="min-h-11 md:min-h-9"
                :disabled="trying"
                @click="tryIt">
                Try it on a sample sentence
            </Button>
            <Button
                v-if="extractor.cached.value || state === 'ready'"
                variant="ghost"
                class="min-h-11 text-destructive md:min-h-9"
                @click="extractor.remove()">
                Remove from this device
            </Button>
        </div>

        <div
            v-if="sample"
            class="rounded-md border p-2 text-sm">
            <p class="italic text-muted-foreground">“{{ SAMPLE }}”</p>
            <p v-if="!sample.length">No suggestions.</p>
            <ul v-else>
                <li
                    v-for="s in sample"
                    :key="s.start">
                    ✨ <b>{{ s.text }}</b> looks like a {{ s.kind }} ({{ s.confidence.toFixed(2) }})
                </li>
            </ul>
        </div>
    </Card>
</template>

<script setup lang="ts">
    import type { SuggestionsSetting } from "~/utils/extraction/deviceSetting";
    import { formatMegabytes, type SuggestionsConfig } from "~/utils/extraction/modelSource";
    import type { ModelSpan } from "~/utils/extraction/spans";

    const OPTIONS: { value: SuggestionsSetting; label: string; hint: string }[] = [
        { value: "off", label: "Off", hint: "No suggestions on this device, and nothing is downloaded." },
        {
            value: "ask",
            label: "Ask",
            hint: "Ask before the one-time download. After that it loads by itself, except on mobile data.",
        },
        { value: "automatic", label: "Automatic", hint: "Download and load without asking, except on mobile data." },
    ];
    // Invented, like 23a's test set.
    const SAMPLE = "met rellan at the gates of Greyhollow Keep, and the Ember Court's spies watched.";

    const config = useRuntimeConfig().public.suggestions as SuggestionsConfig;
    const extractor = useExtractor();
    const state = extractor.state;
    const progress = extractor.progress;
    const busy = computed(() => state.value === "downloading" || state.value === "loading");

    function download() {
        void extractor.ensure({ consent: true });
    }

    const trying = ref(false);
    const sample = ref<ModelSpan[] | null>(null);
    async function tryIt() {
        trying.value = true;
        try {
            sample.value = await extractor.extract("me:sample", SAMPLE);
        } finally {
            trying.value = false;
        }
    }
</script>
