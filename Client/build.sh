#!/bin/bash
# Builds the browser client: the real Core in WebAssembly + the touch client + the game data, into Client/dist (a static site: any static host serves it).
# Needs: .NET 8 SDK with the "wasm-experimental" workload (dotnet workload install wasm-experimental). No other dependency; nothing is downloaded at run time.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"; ROOT="$(cd "$HERE/.." && pwd)"; OUT="${1:-$HERE/dist}"
rm -rf "$OUT" "$HERE/.pub"; mkdir -p "$OUT/data"
dotnet publish "$HERE/Fs27.Wasm/Fs27.Wasm.csproj" -c Release -o "$HERE/.pub" -nologo -v q
cp -r "$HERE/.pub/wwwroot/_framework" "$OUT/_framework"
# the host compresses on the fly (the runtime asks for the plain names); the three ICU files stay: which one loads depends on the phone's language
rm -f "$OUT"/_framework/*.br "$OUT"/_framework/*.gz
cp "$HERE/web/index.html" "$HERE/web/style.css" "$HERE/web/game.js" "$HERE/web/vercel.json" "$OUT/"
# the authoring files the database loads (the bridge says which ones: Fs27.Wasm/DataFiles.cs)
for f in $(grep -o '"FS27_[A-Z0-9_]*\.\(md\|json\)"' "$HERE/Fs27.Wasm/DataFiles.cs" | tr -d '"' | sort -u); do cp "$ROOT/Docs/authoring/$f" "$OUT/data/$f"; done
rm -rf "$HERE/.pub"
echo "built: $OUT ($(du -sh "$OUT" | cut -f1)); serve it with any static server, e.g.: node Client/test/serve.mjs $OUT 8080"
