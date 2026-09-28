// Step 23b: the messages between `useExtractor` and `workers/extractor.worker.ts`.
import type { ModelSource } from "./modelSource";
import type { ModelSpan } from "./spans";

export type ToWorker =
    | { type: "load"; source: ModelSource }
    | { type: "extract"; id: number; text: string };

export type FromWorker =
    | { type: "progress"; loaded: number; total: number; fromCache: boolean }
    | { type: "ready"; fromCache: boolean; ms: number }
    | { type: "result"; id: number; spans: ModelSpan[] }
    | { type: "error"; id?: number; message: string; checksum?: boolean };
