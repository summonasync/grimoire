#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "usage: install.sh <archive-path-or-url> [--bin-dir DIR] [--web-root DIR]" >&2
  exit 2
}

[ $# -ge 1 ] || usage
SOURCE="$1"
shift
BIN_DIR="$HOME/.local/bin"
WEB_ROOT="$HOME/.grimoire/web"
while [ $# -gt 0 ]; do
  case "$1" in
    --bin-dir) BIN_DIR="$2"; shift 2 ;;
    --web-root) WEB_ROOT="$2"; shift 2 ;;
    *) usage ;;
  esac
done

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

case "$SOURCE" in
  http://*|https://*)
    echo "downloading $SOURCE"
    curl -fL --retry 2 -o "$TMP/$(basename "${SOURCE%%\?*}")" "$SOURCE"
    ARCHIVE="$TMP/$(basename "${SOURCE%%\?*}")"
    ;;
  *)
    [ -f "$SOURCE" ] || { echo "archive not found: $SOURCE" >&2; exit 1; }
    ARCHIVE="$SOURCE"
    ;;
esac

case "$ARCHIVE" in
  *.zip)
    if command -v unzip >/dev/null 2>&1; then
      unzip -q "$ARCHIVE" -d "$TMP/x"
    else
      python3 -m zipfile -e "$ARCHIVE" "$TMP/x"
    fi
    ;;
  *.tar.gz) mkdir -p "$TMP/x"; tar -xzf "$ARCHIVE" -C "$TMP/x" ;;
  *) echo "unsupported archive shape: $ARCHIVE" >&2; exit 1 ;;
esac

INNER=$(find "$TMP/x" -maxdepth 2 -type d -name 'grimoire-*' | head -1)
[ -n "$INNER" ] || { echo "archive carries no grimoire-<version>-<rid> tree" >&2; exit 1; }
[ -f "$INNER/web/grimoire-web.json" ] || { echo "archive carries no SPA stamp" >&2; exit 1; }

if [ -f "$INNER/grimoire" ]; then BIN_SRC="$INNER/grimoire"; BIN_NAME=grimoire
elif [ -f "$INNER/grimoire.exe" ]; then BIN_SRC="$INNER/grimoire.exe"; BIN_NAME=grimoire.exe
else echo "archive carries no binary" >&2; exit 1; fi

NEW_SHA=$(sha256sum "$BIN_SRC" | awk '{print $1}')
OLD_SHA=""
[ -f "$BIN_DIR/$BIN_NAME" ] && OLD_SHA=$(sha256sum "$BIN_DIR/$BIN_NAME" | awk '{print $1}')
if [ "$NEW_SHA" = "$OLD_SHA" ] && [ -f "$WEB_ROOT/grimoire-web.json" ] \
   && cmp -s "$INNER/web/grimoire-web.json" "$WEB_ROOT/grimoire-web.json"; then
  echo "grimoire unchanged: binary and web stamp already match the archive"
  exit 0
fi

mkdir -p "$BIN_DIR"
install -m 0755 "$BIN_SRC" "$BIN_DIR/$BIN_NAME"
rm -rf "$WEB_ROOT"
mkdir -p "$WEB_ROOT"
cp -a "$INNER/web/." "$WEB_ROOT/"

case ":$PATH:" in
  *":$BIN_DIR:"*) ;;
  *) echo "note: $BIN_DIR is not on PATH; add: export PATH=\"$BIN_DIR:\$PATH\"" ;;
esac
echo "installed $(python3 -c "import json;print(json.load(open('$WEB_ROOT/grimoire-web.json'))['productVersion'])" 2>/dev/null || echo grimoire) -> $BIN_DIR/$BIN_NAME, web -> $WEB_ROOT"
