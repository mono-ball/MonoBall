#!/bin/bash
# Audio Streaming Test Setup Script
# Generates test OGG audio files for streaming tests

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_DATA_DIR="${SCRIPT_DIR}/TestData/Audio"
SFX_DIR="${TEST_DATA_DIR}/SFX"

echo "=========================================="
echo "Audio Streaming Test Setup"
echo "=========================================="
echo ""

# Check if ffmpeg is installed
if ! command -v ffmpeg &> /dev/null; then
    echo "ERROR: ffmpeg is not installed!"
    echo "Please install ffmpeg:"
    echo "  - Ubuntu/Debian: sudo apt-get install ffmpeg"
    echo "  - macOS: brew install ffmpeg"
    echo "  - Windows: Download from https://ffmpeg.org/download.html"
    exit 1
fi

echo "✓ ffmpeg found: $(ffmpeg -version | head -n1)"
echo ""

# Create directories
echo "Creating test data directories..."
mkdir -p "${TEST_DATA_DIR}"
mkdir -p "${SFX_DIR}"
echo "✓ Directories created"
echo ""

# Generate test music file (3 seconds, 440Hz sine wave)
echo "Generating test_music.ogg (3s, 440Hz A4)..."
ffmpeg -y -f lavfi -i "sine=frequency=440:duration=3" \
    -acodec libvorbis -q:a 4 \
    "${TEST_DATA_DIR}/test_music.ogg" \
    -loglevel error
echo "✓ test_music.ogg created ($(stat -c%s "${TEST_DATA_DIR}/test_music.ogg" 2>/dev/null || stat -f%z "${TEST_DATA_DIR}/test_music.ogg") bytes)"

# Generate test loop file (5 seconds, 880Hz sine wave)
echo "Generating test_loop.ogg (5s, 880Hz A5)..."
ffmpeg -y -f lavfi -i "sine=frequency=880:duration=5" \
    -acodec libvorbis -q:a 4 \
    "${TEST_DATA_DIR}/test_loop.ogg" \
    -loglevel error
echo "✓ test_loop.ogg created ($(stat -c%s "${TEST_DATA_DIR}/test_loop.ogg" 2>/dev/null || stat -f%z "${TEST_DATA_DIR}/test_loop.ogg") bytes)"

# Add loop point metadata to test_loop.ogg
if command -v vorbiscomment &> /dev/null; then
    echo "Adding loop point metadata to test_loop.ogg..."
    cat > "${TEST_DATA_DIR}/loop_tags.txt" <<EOF
LOOPSTART=44100
LOOPLENGTH=88200
EOF
    vorbiscomment -w -c "${TEST_DATA_DIR}/loop_tags.txt" "${TEST_DATA_DIR}/test_loop.ogg"
    rm "${TEST_DATA_DIR}/loop_tags.txt"
    echo "✓ Loop point metadata added (LOOPSTART=44100, LOOPLENGTH=88200)"
else
    echo "⚠ vorbiscomment not found - loop point metadata not added"
    echo "  Install vorbis-tools to enable loop points:"
    echo "    - Ubuntu/Debian: sudo apt-get install vorbis-tools"
    echo "    - macOS: brew install vorbis-tools"
fi

# Generate short sound effect (1 second, 1320Hz sine wave)
echo "Generating test_sfx.ogg (1s, 1320Hz E6)..."
ffmpeg -y -f lavfi -i "sine=frequency=1320:duration=1" \
    -acodec libvorbis -q:a 4 \
    "${SFX_DIR}/test_sfx.ogg" \
    -loglevel error
echo "✓ test_sfx.ogg created ($(stat -c%s "${SFX_DIR}/test_sfx.ogg" 2>/dev/null || stat -f%z "${SFX_DIR}/test_sfx.ogg") bytes)"

# Generate long sound effect (3 seconds, 660Hz sine wave)
echo "Generating test_long_sfx.ogg (3s, 660Hz E5)..."
ffmpeg -y -f lavfi -i "sine=frequency=660:duration=3" \
    -acodec libvorbis -q:a 4 \
    "${SFX_DIR}/test_long_sfx.ogg" \
    -loglevel error
echo "✓ test_long_sfx.ogg created ($(stat -c%s "${SFX_DIR}/test_long_sfx.ogg" 2>/dev/null || stat -f%z "${SFX_DIR}/test_long_sfx.ogg") bytes)"

echo ""
echo "=========================================="
echo "Setup Complete!"
echo "=========================================="
echo ""
echo "Generated files:"
echo "  ${TEST_DATA_DIR}/test_music.ogg"
echo "  ${TEST_DATA_DIR}/test_loop.ogg"
echo "  ${SFX_DIR}/test_sfx.ogg"
echo "  ${SFX_DIR}/test_long_sfx.ogg"
echo ""
echo "You can now run the audio streaming tests:"
echo "  dotnet test --filter \"FullyQualifiedName~Streaming\""
echo ""
