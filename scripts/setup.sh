#!/bin/bash
# Setup script for DOS2DE Save Editor on macOS
# Run once after cloning the repo (with --recursive).

set -e

echo "=== DOS2DE Save Editor Setup ==="

if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK 9.0+ required. Install from https://dotnet.microsoft.com/download"
    exit 1
fi

echo ".NET SDK $(dotnet --version)"

echo ""
echo "--- Initializing submodules ---"
git submodule update --init --recursive

echo ""
echo "--- Building ---"
dotnet build Dos2SaveEditor.slnx

echo ""
echo "=== Setup complete ==="
echo "Run:  dotnet run --project src/Dos2SaveEditor"
