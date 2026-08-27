#!/usr/bin/env bash
# Downloads the quantized Phi-3.5-mini-instruct ONNX weights (~2.6GB) used by
# Atlastica.Nlp for local, in-process fact extraction (no external LLM API
# calls). Not committed to git - run this once after cloning.
set -euo pipefail

MODEL_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/phi-3.5-mini-instruct-cpu-int4"
BASE="https://huggingface.co/microsoft/Phi-3.5-mini-instruct-onnx/resolve/main/cpu_and_mobile/cpu-int4-awq-block-128-acc-level-4"

mkdir -p "$MODEL_DIR"
cd "$MODEL_DIR"

FILES=(
  config.json
  genai_config.json
  special_tokens_map.json
  tokenizer.json
  tokenizer_config.json
  phi-3.5-mini-instruct-cpu-int4-awq-block-128-acc-level-4.onnx
  phi-3.5-mini-instruct-cpu-int4-awq-block-128-acc-level-4.onnx.data
)

for f in "${FILES[@]}"; do
  if [ -f "$f" ]; then
    echo "skip $f (already present)"
    continue
  fi
  echo "downloading $f"
  curl -L --fail -o "$f" "$BASE/$f"
done

echo "Model ready at $MODEL_DIR"
