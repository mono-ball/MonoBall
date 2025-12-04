# Debug Console Fonts

This folder contains the bundled font for the PokeSharp debug console.

## Bundled Font: 0xProto Nerd Font

We use **0xProto Nerd Font Mono** for the debug console because:

- **Excellent readability** - designed specifically for terminals and source code
- **Clear character differentiation** - easy to distinguish similar characters
- **Nerd Font glyphs** - includes 10,000+ icons for file types, git status, etc.
- **SIL Open Font License** - free for distribution and commercial use

## Setup Instructions

Download the font from Nerd Fonts:

1. Go to https://www.nerdfonts.com/font-downloads
2. Download "0xProto" (or use the direct link below)
3. Extract the ZIP and copy `0xProtoNerdFontMono-Regular.ttf` to this folder

### Quick Download (macOS/Linux)

```bash
# Download and extract
curl -L -o /tmp/0xProto.zip https://github.com/ryanoasis/nerd-fonts/releases/download/v3.3.0/0xProto.zip
unzip -o /tmp/0xProto.zip -d /tmp/0xProto

# Copy the regular mono variant to this folder
cp "/tmp/0xProto/0xProtoNerdFontMono-Regular.ttf" .

# Cleanup
rm -rf /tmp/0xProto /tmp/0xProto.zip
```

### Quick Download (Windows PowerShell)

```powershell
# Download
Invoke-WebRequest -Uri "https://github.com/ryanoasis/nerd-fonts/releases/download/v3.3.0/0xProto.zip" -OutFile "$env:TEMP\0xProto.zip"

# Extract
Expand-Archive -Path "$env:TEMP\0xProto.zip" -DestinationPath "$env:TEMP\0xProto" -Force

# Copy (run from this directory)
Copy-Item "$env:TEMP\0xProto\0xProtoNerdFontMono-Regular.ttf" .

# Cleanup
Remove-Item "$env:TEMP\0xProto.zip", "$env:TEMP\0xProto" -Recurse -Force
```

## Expected File

After setup, this folder should contain:

- `0xProtoNerdFontMono-Regular.ttf` - The main font file
- `OFL.txt` - SIL Open Font License
- `README.md` - This file

## Fallback Behavior

If the bundled font is not found, `FontLoader` will fall back to system fonts:

- **macOS**: Monaco, Menlo, Courier New
- **Windows**: Consolas, Courier
- **Linux**: Liberation Mono, DejaVu Sans Mono

## License

0xProto is licensed under the **SIL Open Font License 1.1** (see `OFL.txt`).
This allows free use, modification, and distribution with any software.

