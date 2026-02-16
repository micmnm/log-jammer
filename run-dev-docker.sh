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
    echo "Stopping Docker containers..."
    docker compose -f "$SCRIPT_DIR/docker-compose.dev.yml" down
    echo "Done."
}

trap cleanup SIGINT SIGTERM EXIT

echo "========================================"
echo "  Log Jammer - Development (Docker DB)"
echo "========================================"
echo ""
echo "  API:      http://localhost:5000"
echo "  Frontend: http://localhost:5173"
echo "  Scalar:   http://localhost:5000/scalar"
echo "  DB:       localhost:5432 (logjammer/logjammer)"
echo ""
echo "  Press Ctrl+C to stop"
echo "========================================"
echo ""

# Start PostgreSQL with pgvector
echo "Starting PostgreSQL (pgvector)..."
docker compose -f "$SCRIPT_DIR/docker-compose.dev.yml" up -d

echo "Waiting for PostgreSQL to be ready..."
until docker compose -f "$SCRIPT_DIR/docker-compose.dev.yml" exec db pg_isready -U logjammer -d logjammer >/dev/null 2>&1; do
    sleep 1
done
echo "PostgreSQL is ready."
echo ""

# Start .NET API with connection string pointing to local Docker DB
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=logjammer;Username=logjammer;Password=logjammer"
dotnet run --project "$SCRIPT_DIR/src/LogJammer.Api" --launch-profile Development &
API_PID=$!

# Start Vite frontend
npm run dev --prefix "$SCRIPT_DIR/src/frontend" &
FRONTEND_PID=$!

# Wait for either process to exit
wait -n "$API_PID" "$FRONTEND_PID" 2>/dev/null || true
cleanup
