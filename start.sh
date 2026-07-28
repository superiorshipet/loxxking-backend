#!/bin/bash
# ─── LoxxKing Backend Start Script ───────────────────────────────────────────
# Run this script from the project root to start the backend server.
# Usage: ./start.sh
# Or with build: ./start.sh --build

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
API_DLL="$PROJECT_DIR/src/Api/bin/Debug/net10.0/Api.dll"

# Build if requested or dll not found
if [[ "$1" == "--build" ]] || [[ ! -f "$API_DLL" ]]; then
    echo "🔨 Building..."
    dotnet build "$PROJECT_DIR/src/Api/Api.csproj" --no-restore -v quiet
    if [ $? -ne 0 ]; then
        echo "❌ Build failed. Check errors above."
        exit 1
    fi
    echo "✅ Build succeeded."
fi

# Kill any existing instance on port 5196
fuser -k 5196/tcp 2>/dev/null
sleep 1

echo "🚀 Starting LoxxKing backend on http://localhost:5196 ..."
echo "   Press Ctrl+C to stop."
echo ""

dotnet "$API_DLL" --urls "http://0.0.0.0:5196"
