#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
LOG_DIR="$SCRIPT_DIR/logs"

if [ -d "$LOG_DIR" ] && [ -n "$(ls -A "$LOG_DIR" 2>/dev/null)" ]; then
    ARCHIVE="$SCRIPT_DIR/src/SampleLog/logs-$(date +%Y%m%d-%H%M%S).zip"
    echo "Archiving existing logs to $ARCHIVE ..."
    zip -jq "$ARCHIVE" "$LOG_DIR"/*
    rm "$LOG_DIR"/*
    echo "Done. Starting SampleLog."
fi

cd "$SCRIPT_DIR/src/SampleLog"
dotnet run
