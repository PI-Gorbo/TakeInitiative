// The candidates of step 23a, pinned by Hugging Face revision. `fetch-models.mjs` downloads
// these files into `.data/models/{repo}/{revision}/` (git-ignored) and prints their sha256.
// Nothing here is committed but the manifest: never the weights.

export const candidates = [
    {
        id: "gliner-small-v2.1",
        kind: "gliner",
        repo: "onnx-community/gliner_small-v2.1",
        upstream: "urchade/gliner_small-v2.1",
        licence: "Apache-2.0 (upstream model card, urchade/gliner_small-v2.1)",
        revision: "8142fb00740ccea973e64b1272949ff48653df5e",
        model: "onnx/model_int8.onnx",
        tokenizer: "tokenizer.json",
        tokenizerConfig: "tokenizer_config.json",
        extra: ["gliner_config.json"],
        maxWords: 384,
    },
    {
        id: "gliner-multi-v2.1",
        kind: "gliner",
        repo: "onnx-community/gliner_multi-v2.1",
        upstream: "urchade/gliner_multi-v2.1",
        licence: "Apache-2.0 (upstream model card, urchade/gliner_multi-v2.1)",
        revision: "6ddaeb9413b0e71ad8457da1aab378a165b24058",
        model: "onnx/model_int8.onnx",
        tokenizer: "tokenizer.json",
        tokenizerConfig: "tokenizer_config.json",
        extra: ["gliner_config.json"],
        maxWords: 384,
    },
    {
        // The same checkpoint's uint8 export. onnxruntime-web's WASM int8 (s8) kernels lose
        // most of GLiNER v2.1's spans; the u8 weights (what transformers.js calls "q8") do not.
        id: "gliner-small-v2.1-uint8",
        kind: "gliner",
        repo: "onnx-community/gliner_small-v2.1",
        upstream: "urchade/gliner_small-v2.1",
        licence: "Apache-2.0 (upstream model card, urchade/gliner_small-v2.1)",
        revision: "8142fb00740ccea973e64b1272949ff48653df5e",
        model: "onnx/model_uint8.onnx",
        tokenizer: "tokenizer.json",
        tokenizerConfig: "tokenizer_config.json",
        extra: ["gliner_config.json"],
        maxWords: 384,
    },
    {
        id: "gliner-multi-v2.1-uint8",
        kind: "gliner",
        repo: "onnx-community/gliner_multi-v2.1",
        upstream: "urchade/gliner_multi-v2.1",
        licence: "Apache-2.0 (upstream model card, urchade/gliner_multi-v2.1)",
        revision: "6ddaeb9413b0e71ad8457da1aab378a165b24058",
        model: "onnx/model_uint8.onnx",
        tokenizer: "tokenizer.json",
        tokenizerConfig: "tokenizer_config.json",
        extra: ["gliner_config.json"],
        maxWords: 384,
    },
    {
        // The optional fourth row (23a step 2): a GLiNER v2.5 checkpoint with ONNX weights and a
        // permissive licence. A community int8 export of gliner-community/gliner_small-v2.5.
        id: "gliner-small-v2.5",
        kind: "gliner",
        repo: "GG-QandV/gliner_small-v2.5-onnx",
        upstream: "gliner-community/gliner_small-v2.5@f227d3cd637bd4e6757ae143935316d062393341",
        licence: "Apache-2.0 (LICENSE and NOTICE in the repo)",
        revision: "a748820c906f7af707a25bb52411b21b999f8de9",
        model: "model_quantized.onnx",
        tokenizer: "tokenizer.json",
        tokenizerConfig: "tokenizer_config.json",
        extra: ["gliner_config.json", "LICENSE", "NOTICE"],
        maxWords: 384,
    },
];

export const filesOf = (c) => [c.model, c.tokenizer, c.tokenizerConfig, ...c.extra];
