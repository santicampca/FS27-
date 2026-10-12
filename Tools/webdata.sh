#!/bin/bash
# Regenerates the files the web menu reads (Web/menu/public/data/*.json) from the real Core. About 2 minutes (the penalty lab plays ~200,000 real penalties).
# Without this script the same tests only CHECK that the committed files are not stale:  dotnet test Tests/Core.Tests --filter "Name~WebExport"
set -e
cd "$(dirname "$0")/../Tests/Core.Tests"
FS27_WRITE_WEB_DATA=1 dotnet test --nologo -v q --filter "Name~WebExport"
