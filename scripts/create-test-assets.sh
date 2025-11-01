#!/bin/bash

# create-test-assets.sh
# Generates minimal test assets for PokeSharp Phase 1
# Requires: ImageMagick (magick command)

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSETS_DIR="$SCRIPT_DIR/../PokeSharp.Game/Assets"

echo "🎨 Creating test assets for PokeSharp Phase 1..."
echo ""

# Check if ImageMagick is installed
if ! command -v magick &> /dev/null; then
    echo "❌ Error: ImageMagick is not installed."
    echo ""
    echo "Install ImageMagick:"
    echo "  Ubuntu/Debian: sudo apt install imagemagick"
    echo "  macOS:         brew install imagemagick"
    echo "  Windows:       https://imagemagick.org/script/download.php"
    echo ""
    echo "Or create assets manually using docs/test-asset-creation-guide.md"
    exit 1
fi

# Create directories if they don't exist
mkdir -p "$ASSETS_DIR/Tilesets"
mkdir -p "$ASSETS_DIR/Sprites"

echo "📁 Asset directories: OK"
echo ""

# Create test tileset (64x64, 4x4 grid of 16x16 tiles)
echo "🎨 Creating test-tileset.png (64x64, 16 tiles)..."
magick -size 64x64 xc:white \
  -fill "#228b22" -draw "rectangle 0,0 15,15" \
  -fill "#8b4513" -draw "rectangle 16,0 31,15" \
  -fill "#4682b4" -draw "rectangle 32,0 47,15" \
  -fill "#daa520" -draw "rectangle 48,0 63,15" \
  -fill "#696969" -draw "rectangle 0,16 15,31" \
  -fill "#ff6347" -draw "rectangle 16,16 31,31" \
  -fill "#9370db" -draw "rectangle 32,16 47,31" \
  -fill "#20b2aa" -draw "rectangle 48,16 63,31" \
  -fill "#ffa500" -draw "rectangle 0,32 15,47" \
  -fill "#dc143c" -draw "rectangle 16,32 31,47" \
  -fill "#00ced1" -draw "rectangle 32,32 47,47" \
  -fill "#ff1493" -draw "rectangle 48,32 63,47" \
  -fill "#32cd32" -draw "rectangle 0,48 15,63" \
  -fill "#ff4500" -draw "rectangle 16,48 31,63" \
  -fill "#1e90ff" -draw "rectangle 32,48 47,63" \
  -fill "#ffd700" -draw "rectangle 48,48 63,63" \
  "$ASSETS_DIR/Tilesets/test-tileset.png"

echo "✅ test-tileset.png created (64x64, 16 colored tiles)"
echo ""

# Create player sprite (16x16, simple character)
echo "🎨 Creating player.png (16x16)..."
magick -size 16x16 xc:none \
  -fill "#ff6347" -draw "circle 8,5 8,2" \
  -fill "#4682b4" -draw "rectangle 5,6 11,12" \
  -fill "#ffd700" -draw "rectangle 4,8 6,14" \
  -fill "#ffd700" -draw "rectangle 10,8 12,14" \
  "$ASSETS_DIR/Sprites/player.png"

echo "✅ player.png created (16x16, simple character)"
echo ""

# Verify assets
echo "📋 Verifying assets..."
if [ -f "$ASSETS_DIR/Tilesets/test-tileset.png" ]; then
    SIZE=$(file "$ASSETS_DIR/Tilesets/test-tileset.png" | grep -oP '\d+ x \d+')
    echo "  ✅ test-tileset.png: $SIZE"
else
    echo "  ❌ test-tileset.png: NOT FOUND"
fi

if [ -f "$ASSETS_DIR/Sprites/player.png" ]; then
    SIZE=$(file "$ASSETS_DIR/Sprites/player.png" | grep -oP '\d+ x \d+')
    echo "  ✅ player.png: $SIZE"
else
    echo "  ❌ player.png: NOT FOUND"
fi

echo ""
echo "🎉 Test assets created successfully!"
echo ""
echo "Next steps:"
echo "  1. cd PokeSharp.Game"
echo "  2. dotnet run"
echo "  3. Use WASD or Arrow Keys to move"
echo ""
echo "Expected output:"
echo "  ✅ Asset manifest loaded successfully"
echo "  ✅ Loaded test map: test-map (20x15 tiles)"
echo "  ✅ Created player entity"
echo "  🎮 Use WASD or Arrow Keys to move!"
