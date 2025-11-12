#!/bin/bash

# Pokemon Emerald NPC Sprite Extraction Script
# This script extracts all NPC sprites from pokeemerald and prepares them for PokeSharp.Game

set -e

# Configuration
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
POKEEMERALD_PATH="$PROJECT_ROOT/pokeemerald"
OUTPUT_PATH="$PROJECT_ROOT/PokeSharp.Game/Assets/Sprites/NPCs"
EXTRACTOR_PATH="$SCRIPT_DIR/SpriteExtractor"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "======================================"
echo "  Pokemon Emerald Sprite Extractor  "
echo "======================================"
echo ""

# Check if pokeemerald directory exists
if [ ! -d "$POKEEMERALD_PATH" ]; then
    echo "Error: pokeemerald directory not found at $POKEEMERALD_PATH"
    echo "Please ensure the pokeemerald source code is in the project root."
    exit 1
fi

# Check if sprite source directory exists
SPRITE_SOURCE="$POKEEMERALD_PATH/graphics/object_events/pics/people"
if [ ! -d "$SPRITE_SOURCE" ]; then
    echo "Error: Sprite source directory not found at $SPRITE_SOURCE"
    exit 1
fi

echo -e "${GREEN}✓${NC} Found pokeemerald at: $POKEEMERALD_PATH"
echo -e "${GREEN}✓${NC} Found sprites at: $SPRITE_SOURCE"
echo ""

# Count available sprites
SPRITE_COUNT=$(find "$SPRITE_SOURCE" -name "*.png" | wc -l)
echo "Found $SPRITE_COUNT sprite files to extract"
echo ""

# Create output directory
mkdir -p "$OUTPUT_PATH"
echo -e "${GREEN}✓${NC} Output directory: $OUTPUT_PATH"
echo ""

# Build and run extractor
echo -e "${YELLOW}Building sprite extractor...${NC}"
cd "$EXTRACTOR_PATH"
dotnet build -c Release

echo ""
echo -e "${YELLOW}Extracting sprites...${NC}"
dotnet run --no-build -c Release -- "$POKEEMERALD_PATH" "$OUTPUT_PATH"

echo ""
echo -e "${GREEN}======================================"
echo "  Extraction Complete!              "
echo "======================================${NC}"
echo ""
echo "Sprites extracted to: $OUTPUT_PATH"
echo "Master manifest: $OUTPUT_PATH/npc_sprites_manifest.json"
echo ""
echo "You can now use these sprites in PokeSharp.Game!"

