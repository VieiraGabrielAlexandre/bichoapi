#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.."; pwd)"
APP="$ROOT/src/BichoApi"
OUT="$ROOT/out/publish"

rm -rf "$OUT"
dotnet publish "$APP" -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true -o "$OUT"
cd "$OUT"
tar -czf "$ROOT/out/bichoapp.tar.gz" .
echo "Artefato em: $ROOT/out/bichoapp.tar.gz"
