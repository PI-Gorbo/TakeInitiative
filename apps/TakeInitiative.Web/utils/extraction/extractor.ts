// Step 23b: GLiNER (span mode, "markerV0") over onnxruntime and `@huggingface/tokenizers`, ported
// from 23a's `scripts/extraction-spike/lib/gliner.mjs`. It follows GLiNER's Python processor: the
// prompt is `<<ENT>> label … <<SEP>>` then the note's words, each word tokenised on its own, the
// first sub-token of each word marked in `words_mask`, spans every (start, start + width) up to
// `maxWidth` words, and decoding greedy with no overlap.
//
// The runtime and the tokenizer are passed in (structural types below), so this file imports
// neither: only `workers/extractor.worker.ts` does, which keeps them out of every page chunk.

import { LABELS, SUGGESTION_KINDS, finalSpans, mask, sentences, type ModelSpan, type RawSpan, type SuggestionPass } from "./spans";

/** The parts of `onnxruntime-web` the extractor uses. */
export interface OrtLike {
    Tensor: new (type: "int64" | "bool", data: BigInt64Array | Uint8Array, dims: number[]) => unknown;
}
export interface SessionLike {
    run(feeds: Record<string, unknown>): Promise<Record<string, { data: ArrayLike<number> }>>;
}
/** The parts of `@huggingface/tokenizers`' `Tokenizer` the extractor uses. */
export interface TokenizerLike {
    token_to_id(token: string): number | undefined;
    encode(text: string, options: { add_special_tokens: boolean }): { ids: number[] };
}

export interface ExtractorOptions {
    ort: OrtLike;
    session: SessionLike;
    tokenizer: TokenizerLike;
    /** GLiNER's `max_width`: the longest span, in words. */
    maxWidth: number;
    /** GLiNER's `max_len`: the most words in one pass. */
    maxWords: number;
    /** The encoder's limit, in sub-tokens, prompt included. */
    maxTokens: number;
    /** The model's pinned threshold: the default pass, when `extract` is not given one. */
    threshold: number;
}

type Word = [text: string, start: number, end: number];

// GLiNER's whitespace splitter (`\w+(?:[-_]\w+)*|\S`), Unicode-aware.
const SPLIT = /[\p{L}\p{N}_]+(?:[-_][\p{L}\p{N}_]+)*|\S/gu;

const sigmoid = (x: number) => 1 / (1 + Math.exp(-x));

export function createExtractor({ ort, session, tokenizer, maxWidth, maxWords, maxTokens, threshold }: ExtractorOptions) {
    const cls = tokenizer.token_to_id("[CLS]") ?? 1;
    const sep = tokenizer.token_to_id("[SEP]") ?? 2;
    const cache = new Map<string, number[]>();
    const pieces = (word: string) => {
        let ids = cache.get(word);
        if (!ids) {
            ids = tokenizer.encode(word, { add_special_tokens: false }).ids;
            if (cache.size > 50_000) cache.clear();
            cache.set(word, ids);
        }
        return ids;
    };

    function words(text: string, from: number, to: number): Word[] {
        const out: Word[] = [];
        for (const m of text.slice(from, to).matchAll(SPLIT)) {
            out.push([m[0], from + m.index!, from + m.index! + m[0].length]);
        }
        return out;
    }

    // The note in chunks of whole sentences that fit the model (words and sub-tokens).
    function chunks(text: string, labels: string[]): Word[][] {
        const prompt = labels.reduce((n, l) => n + 1 + pieces(l).length, 1) + 2;
        const all: { words: Word[]; tokens: number }[] = [];
        let current: { words: Word[]; tokens: number } | null = null;
        for (const [s, e] of sentences(text)) {
            const w = words(text, s, e);
            const tokens = w.reduce((n, [x]) => n + pieces(x).length, 0);
            if (current && (current.words.length + w.length > maxWords || current.tokens + tokens + prompt > maxTokens)) {
                all.push(current);
                current = null;
            }
            current = current ?? { words: [], tokens: 0 };
            current.words.push(...w);
            current.tokens += tokens;
        }
        if (current) all.push(current);
        // A single sentence too long for the model is cut by words.
        return all.flatMap((c) => {
            if (c.words.length <= maxWords && c.tokens + prompt <= maxTokens) return [c.words];
            const cut: Word[][] = [];
            let part: Word[] = [];
            let n = 0;
            for (const w of c.words) {
                const t = pieces(w[0]).length;
                if (part.length && (part.length >= maxWords || n + t + prompt > maxTokens)) {
                    cut.push(part);
                    part = [];
                    n = 0;
                }
                part.push(w);
                n += t;
            }
            if (part.length) cut.push(part);
            return cut;
        });
    }

    // `at` is the pass's threshold, which is the pinned `threshold` unless `extract` was given a pass.
    async function runChunk(w: Word[], labels: string[], at: number): Promise<RawSpan[]> {
        const ids: number[] = [cls];
        const wordsMask: number[] = [0];
        for (const l of labels) {
            for (const t of ["<<ENT>>", l]) {
                for (const id of pieces(t)) {
                    ids.push(id);
                    wordsMask.push(0);
                }
            }
        }
        for (const id of pieces("<<SEP>>")) {
            ids.push(id);
            wordsMask.push(0);
        }
        w.forEach(([word], i) => {
            pieces(word).forEach((id, k) => {
                ids.push(id);
                wordsMask.push(k === 0 ? i + 1 : 0);
            });
        });
        ids.push(sep);
        wordsMask.push(0);

        const n = w.length;
        const spanIdx = new BigInt64Array(n * maxWidth * 2);
        const spanMask = new Uint8Array(n * maxWidth);
        for (let i = 0; i < n; i++) {
            for (let j = 0; j < maxWidth; j++) {
                const k = i * maxWidth + j;
                const ok = i + j < n;
                spanMask[k] = ok ? 1 : 0;
                spanIdx[2 * k] = ok ? BigInt(i) : 0n;
                spanIdx[2 * k + 1] = ok ? BigInt(i + j) : 0n;
            }
        }
        const L = ids.length;
        const feeds = {
            input_ids: new ort.Tensor("int64", BigInt64Array.from(ids, BigInt), [1, L]),
            attention_mask: new ort.Tensor("int64", new BigInt64Array(L).fill(1n), [1, L]),
            words_mask: new ort.Tensor("int64", BigInt64Array.from(wordsMask, BigInt), [1, L]),
            text_lengths: new ort.Tensor("int64", BigInt64Array.from([BigInt(n)]), [1, 1]),
            span_idx: new ort.Tensor("int64", spanIdx, [1, n * maxWidth, 2]),
            span_mask: new ort.Tensor("bool", spanMask, [1, n * maxWidth]),
        };
        const out = await session.run(feeds);
        const logits = out.logits.data; // [1, n, maxWidth, C]
        const C = labels.length;
        const found: { i: number; e: number; c: number; p: number }[] = [];
        for (let i = 0; i < n; i++) {
            for (let j = 0; j < maxWidth && i + j < n; j++) {
                for (let c = 0; c < C; c++) {
                    const p = sigmoid(logits[(i * maxWidth + j) * C + c]);
                    if (p >= at) found.push({ i, e: i + j, c, p });
                }
            }
        }
        // Greedy, flat: the most probable first, nothing overlapping it after.
        found.sort((a, b) => b.p - a.p);
        const kept: typeof found = [];
        for (const f of found) {
            if (!kept.some((k) => !(f.e < k.i || f.i > k.e))) kept.push(f);
        }
        return kept.map((f) => ({
            start: w[f.i][1],
            length: w[f.e][2] - w[f.i][1],
            kind: SUGGESTION_KINDS[f.c],
            confidence: f.p,
        }));
    }

    return {
        /**
         * The spans the note offers (masked first, tidied after), in text order. `pass` is 23f's
         * depth: without one the model's pinned threshold and span cap apply, which is every
         * automatic read. A deeper pass costs the same inference — only the two cut-offs move —
         * so re-reading one note is cheap.
         */
        async extract(text: string, pass?: SuggestionPass): Promise<ModelSpan[]> {
            const at = pass?.threshold ?? threshold;
            const masked = mask(text);
            const raw: RawSpan[] = [];
            for (const w of chunks(masked, LABELS)) {
                if (w.length) raw.push(...(await runChunk(w, LABELS, at)));
            }
            return finalSpans(text, raw, at, pass?.max);
        },
    };
}
