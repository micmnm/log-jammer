#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

API_PID=""
FRONTEND_PID=""

cleanup() {
    echo ""
    echo "Shutting down..."
    [ -n "$FRONTEND_PID" ] && kill "$FRONTEND_PID" 2>/dev/null && wait "$FRONTEND_PID" 2>/dev/null
    [ -n "$API_PID" ] && kill "$API_PID" 2>/dev/null && wait "$API_PID" 2>/dev/null
    echo "Done."
}

trap cleanup SIGINT SIGTERM EXIT

echo "========================================"
echo "  Log Jammer - Development"
echo "========================================"
echo ""
echo "  API:      http://localhost:5050"
echo "  Frontend: http://localhost:5173"
echo "  Scalar:   http://localhost:5050/scalar"
echo ""
echo "  Press Ctrl+C to stop"
echo "========================================"
echo ""

# Start .NET API
dotnet run --project "$SCRIPT_DIR/src/LogJammer.Api" --launch-profile Development &
API_PID=$!

# Start Vite frontend
npm run dev --prefix "$SCRIPT_DIR/src/frontend" &
FRONTEND_PID=$!

# Wait for either process to exit
wait "$API_PID" "$FRONTEND_PID" 2>/dev/null || true
cleanup
