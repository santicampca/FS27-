#!/bin/bash
# Full verification of the browser client: build it, play the same full match natively and in a real (headless) Chromium with touch emulation, and compare.
# Needs: .NET 8 SDK + workload wasm-experimental, node >= 18, a Chromium (CHROME or PLAYWRIGHT_BROWSERS_PATH) and playwright-core (PLAYWRIGHT_CORE=/path/to/index.mjs). None of them is part of the game.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"; ROOT="$(cd "$HERE/../.." && pwd)"; DIST="${DIST:-/tmp/fs27-client-dist}"
"$ROOT/Client/build.sh" "$DIST" | tail -1
NATIVE=$(cd "$HERE/Native" && dotnet run -c Release -- "$ROOT/Docs/authoring" | tee /dev/stderr | grep '^checksum=' | cut -d= -f2)
EXPECT_CHECKSUM="$NATIVE" node "$HERE/smoke.mjs" "$DIST" "${SHOT:-/tmp/fs27-play.png}"
