# /// script
# requires-python = "==3.12.*"
# dependencies = [
#     "gliner==0.2.28",
#     "torch==2.14.0",
#     "transformers==5.13.1",
#     "huggingface-hub==1.33.0",
#     "onnx==1.23.0",
#     "onnxruntime==1.30.0",
# ]
# ///
"""
Step 23b: build the suggestion model's ONNX weights from the official upstream checkpoint.

    uv run scripts/gliner/export.py [--out DIR]

Downloads `gliner-community/gliner_small-v2.5` (Apache-2.0) at the revision pinned below,
exports it with the GLiNER package's own `export_to_onnx`, quantises it with onnxruntime's
dynamic int8 quantiser (QUInt8 weights, what the package does), and writes the files the web
app serves into `.data/models/export/{model}/{revision}/` (git-ignored):

    model_quantized.onnx, tokenizer.json, tokenizer_config.json, gliner_config.json, LICENSE,
    NOTICE

then prints each file's bytes and sha256. `apps/TakeInitiative.Web/suggestion-model.json`
pins those sha256s; `pnpm models:fetch` refuses a file that differs. Nothing here is committed but this script.

The export is reproducible on the same platform with these pinned package versions. Another
platform (Linux x86 in CI or Docker) may serialise the float export differently, so deploys do
not re-export: they fetch the files this script produced from the mirror named in
`suggestion-model.json` and check them against the pins. (On 2026-09-28 this export on an
Apple-silicon Mac reproduced the community export `GG-QandV/gliner_small-v2.5-onnx`, made on
Linux with torch 2.13, byte for byte: same model and tokenizer sha256s.)
"""

import argparse
import hashlib
import json
import shutil
import tempfile
from pathlib import Path

UPSTREAM = "gliner-community/gliner_small-v2.5"
REVISION = "f227d3cd637bd4e6757ae143935316d062393341"
MODEL = "gliner_small-v2.5"

ROOT = Path(__file__).resolve().parents[2]

NOTICE = f"""gliner_small-v2.5, ONNX int8 export for TakeInitiative
=====================================================

Source model : {UPSTREAM} (https://huggingface.co/{UPSTREAM})
Revision     : {REVISION}
Licence      : Apache-2.0 (see LICENSE)
Authors      : Urchade Zaratiana et al., the GLiNER project (https://github.com/urchade/GLiNER)
Exported by  : TakeInitiative scripts/gliner/export.py (gliner 0.2.28, torch 2.14.0,
               onnxruntime 1.30.0)

The weights are the upstream ones. Only the serialisation format (ONNX, opset 19) and the
dynamic int8 quantisation (onnxruntime.quantization.quantize_dynamic, QUInt8) are ours. The
model was not fine-tuned or otherwise changed.
"""


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path, default=ROOT / ".data/models/export" / MODEL / REVISION)
    args = parser.parse_args()

    import torch
    from gliner import GLiNER
    from huggingface_hub import hf_hub_download, snapshot_download

    torch.manual_seed(0)
    torch.use_deterministic_algorithms(True)

    local = snapshot_download(UPSTREAM, revision=REVISION)
    model = GLiNER.from_pretrained(local, load_tokenizer=True)
    model.eval()

    out: Path = args.out
    out.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        paths = model.export_to_onnx(
            tmp,
            onnx_filename="model.onnx",
            quantized_filename="model_quantized.onnx",
            quantize=True,
            opset=19,
        )
        if not paths.get("quantized_path"):
            raise SystemExit("quantisation failed")
        shutil.copyfile(paths["quantized_path"], out / "model_quantized.onnx")
        for name in ["gliner_config.json", "tokenizer_config.json"]:
            shutil.copyfile(Path(tmp) / name, out / name)
        tok = Path(tmp) / "tokenizer.json"
        if not tok.exists():
            raise SystemExit("the tokenizer was not saved as tokenizer.json")
        shutil.copyfile(tok, out / "tokenizer.json")

    try:
        licence = Path(hf_hub_download(UPSTREAM, "LICENSE", revision=REVISION))
        shutil.copyfile(licence, out / "LICENSE")
    except Exception:
        # The upstream repo states Apache-2.0 in its model card; ship the licence text itself.
        import urllib.request

        with urllib.request.urlopen("https://www.apache.org/licenses/LICENSE-2.0.txt") as r:
            (out / "LICENSE").write_bytes(r.read())
    (out / "NOTICE").write_text(NOTICE, encoding="utf-8")

    files = []
    for name in [
        "model_quantized.onnx",
        "tokenizer.json",
        "tokenizer_config.json",
        "gliner_config.json",
        "LICENSE",
        "NOTICE",
    ]:
        p = out / name
        files.append({"path": name, "bytes": p.stat().st_size, "sha256": sha256(p)})
    print(json.dumps({"model": MODEL, "upstream": UPSTREAM, "revision": REVISION, "files": files}, indent=2))
    print(f"wrote {out}")


if __name__ == "__main__":
    main()
