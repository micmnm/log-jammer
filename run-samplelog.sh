#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/src/SampleLog"

LOG_DIR="./logs"

if [ -d "$LOG_DIR" ] && [ -n "$(ls -A "$LOG_DIR" 2>/dev/null)" ]; then
    ARCHIVE="logs-$(date +%Y%m%d-%H%M%S).zip"
    echo "Archiving existing logs to $ARCHIVE ..."
    zip -jq "$ARCHIVE" "$LOG_DIR"/*
    rm "$LOG_DIR"/*
    echo "Done. Starting SampleLog."
fi

dotnet run
