#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -1)
RIDS=(win-x64 linux-x64 osx-arm64)

if [ "${PACK_SKIP_WEB_BUILD:-0}" != "1" ]; then
  (cd web && npm ci && npm run build)
fi
STAMP=$(python3 -c "import json; print(json.load(open('web/out/grimoire-web.json'))['productVersion'])")
if [ "$STAMP" != "$VERSION" ]; then
  echo "refusing to pack: SPA stamp $STAMP != assembly version $VERSION" >&2
  exit 1
fi

mkdir -p dist
for rid in "${RIDS[@]}"; do
  out="dist/publish/$rid"
  dotnet publish src/Grimoire.Cli -c Release -r "$rid" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true -o "$out"

  stage="dist/stage/$rid/grimoire-$VERSION-$rid"
  rm -rf "$stage"
  mkdir -p "$stage"
  if [ "$rid" = "win-x64" ]; then
    cp "$out/Grimoire.Cli.exe" "$stage/grimoire.exe"
  else
    cp "$out/Grimoire.Cli" "$stage/grimoire"
  fi
  cp -r web/out "$stage/web"
  (cd "$stage" && find . -type f ! -name SHA256SUMS -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS)

  if [ "$rid" = "win-x64" ]; then
    (cd "dist/stage/$rid" && python3 -m zipfile -c "../../grimoire-$VERSION-$rid.zip" "grimoire-$VERSION-$rid")
  else
    tar -czf "dist/grimoire-$VERSION-$rid.tar.gz" -C "dist/stage/$rid" "grimoire-$VERSION-$rid"
  fi
  echo "packed dist/grimoire-$VERSION-$rid ($rid)"
done
echo "pack complete: $VERSION, stamp $STAMP"
