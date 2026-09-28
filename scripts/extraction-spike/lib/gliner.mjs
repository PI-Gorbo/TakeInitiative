// A hand-written GLiNER (span mode, "markerV0") runner over any onnxruntime (`onnxruntime-node`
// in Node, `onnxruntime-web` in the browser) and `@huggingface/tokenizers`. It follows GLiNER's
// Python processor: the prompt is `<<ENT>> label … <<SEP>>` then the note's words, each word is
// tokenised on its own, the first sub-token of each word is marked in `words_mask`, spans are
// every (start, start + width) up to `max_width` words, and decoding is greedy with no overlap.
// Why not the `gliner` npm package: 0.0.19 pins onnxruntime-web 1.19.2 and @xenova/transformers
// 2.17.2 (2024), and its span mask never masks anything (it clamps before comparing).

import { sentences } from "./text.mjs";

// GLiNER's whitespace splitter (`\w+(?:[-_]\w+)*|\S`), Unicode-aware.
const SPLIT = /[\p{L}\p{N}_]+(?:[-_][\p{L}\p{N}_]+)*|\S/gu;

const sigmoid = (x) => 1 / (1 + Math.exp(-x));

export function createGliner({ ort, session, tokenizer, config, maxTokens = 512 }) {
    const maxWidth = config.max_width ?? 12;
    const maxWords = config.max_len ?? 384;
    const cls = tokenizer.token_to_id("[CLS]");
    const sep = tokenizer.token_to_id("[SEP]");
    const cache = new Map();
    const pieces = (word) => {
        let ids = cache.get(word);
        if (!ids) {
            ids = tokenizer.encode(word, { add_special_tokens: false }).ids;
            cache.set(word, ids);
        }
        return ids;
    };

    function words(text, from, to) {
        const out = [];
        SPLIT.lastIndex = 0;
        for (const m of text.slice(from, to).matchAll(SPLIT)) {
            out.push([m[0], from + m.index, from + m.index + m[0].length]);
        }
        return out;
    }

    // The note split into chunks of whole sentences that fit the model (words and sub-tokens).
    function chunks(text, labels) {
        const prompt = labels.reduce((n, l) => n + 1 + pieces(l).length, 1) + 2;
        const all = [];
        let current = null;
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
            const cut = [];
            let part = [];
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

    async function runChunk(w, labels, threshold) {
        const ids = [cls];
        const wordsMask = [0];
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
        const found = [];
        for (let i = 0; i < n; i++) {
            for (let j = 0; j < maxWidth && i + j < n; j++) {
                for (let c = 0; c < C; c++) {
                    const p = sigmoid(logits[(i * maxWidth + j) * C + c]);
                    if (p >= threshold) found.push({ i, e: i + j, c, p });
                }
            }
        }
        // Greedy, flat: the most probable first, nothing overlapping it after.
        found.sort((a, b) => b.p - a.p);
        const kept = [];
        for (const f of found) {
            if (!kept.some((k) => !(f.e < k.i || f.i > k.e))) kept.push(f);
        }
        return kept.map((f) => ({
            start: w[f.i][1],
            length: w[f.e][2] - w[f.i][1],
            kind: labels[f.c],
            confidence: f.p,
        }));
    }

    return {
        /** Spans over `threshold` in `text` (already masked), with character offsets into it. */
        async extract(text, labels, threshold = 0.5) {
            const spans = [];
            for (const w of chunks(text, labels)) {
                if (w.length) spans.push(...(await runChunk(w, labels, threshold)));
            }
            return spans.map((s) => ({ ...s, text: text.slice(s.start, s.start + s.length) }));
        },
    };
}
