#!/usr/bin/env bash
# Builds the game content, runs EditMode + PlayMode tests and a Windows/Linux/macOS player build headlessly.
# Usage: UNITY=/path/to/Unity ./Tools/Unity/run_all.sh [win|linux|mac]
set -u
PROJ="$(cd "$(dirname "$0")/../.." && pwd)"
U="${UNITY:?set UNITY to the Unity 6.3 editor executable}"
mkdir -p "$PROJ/Logs" "$PROJ/Docs/Generated"
"$U" -batchmode -projectPath "$PROJ" -executeMethod Kendo.EditorTools.Setup.KendoGameBuilder.BuildFromCommandLine -logFile "$PROJ/Logs/build_game.log"; B=$?
"$U" -batchmode -projectPath "$PROJ" -runTests -testPlatform EditMode -testResults "$PROJ/Logs/editmode.xml" -logFile "$PROJ/Logs/editmode.log"; E=$?
"$U" -batchmode -projectPath "$PROJ" -runTests -testPlatform PlayMode -testResults "$PROJ/Logs/playmode.xml" -logFile "$PROJ/Logs/playmode.log"; P=$?
T="${1:-win}"
"$U" -batchmode -quit -projectPath "$PROJ" -executeMethod Kendo.EditorTools.Setup.KendoPlayerBuild.Build -target "$T" -logFile "$PROJ/Logs/player_build.log"; X=$?
{
  echo "# Unity results ($(date))"
  echo "- Build Game exit code: $B (details: Docs/Generated/BuildReport.md, Logs/build_game.log)"
  echo "- EditMode tests exit code: $E (Logs/editmode.xml)"
  echo "- PlayMode tests exit code: $P (Logs/playmode.xml)"
  echo "- Player build ($T) exit code: $X (Builds/, Logs/player_build.log)"
  grep -h -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' "$PROJ"/Logs/*mode.xml 2>/dev/null | sed 's/^/- /'
} > "$PROJ/Docs/Generated/UnityResults.md"
cat "$PROJ/Docs/Generated/UnityResults.md"
