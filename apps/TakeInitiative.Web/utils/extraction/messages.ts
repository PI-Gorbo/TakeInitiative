// Step 23b: the messages between `useExtractor` and `workers/extractor.worker.ts`.
import type { ModelSource } from "./modelSource";
import type { ModelSpan, SuggestionPass } from "./spans";

export type ToWorker =
    | { type: "load"; source: ModelSource }
    // `pass` is 23f's depth; without it the model's pinned threshold and span cap apply.
    | { type: "extract"; id: number; text: string; pass?: SuggestionPass };

export type FromWorker =
    | { type: "progress"; loaded: number; total: number; fromCache: boolean }
    | { type: "ready"; fromCache: boolean; ms: number }
    | { type: "result"; id: number; spans: ModelSpan[] }
    | { type: "error"; id?: number; message: string; checksum?: boolean };
