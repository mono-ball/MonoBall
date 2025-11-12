using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SpriteExtractor;

class Program
{
    static async Task Main(string[] args)
    {
        var pokeemeraldPath = args.Length > 0 ? args[0] : "../pokeemerald";
        var spritesBasePath = args.Length > 1 ? args[1] : "../PokeSharp.Game/Assets/Sprites";

        Console.WriteLine("=== Pokemon Emerald Sprite Extractor ===");
        Console.WriteLine($"Source: {pokeemeraldPath}");
        Console.WriteLine($"Output: {spritesBasePath}");
        Console.WriteLine();

        var extractor = new SpriteExtractor(pokeemeraldPath, spritesBasePath);
        await extractor.ExtractAllSprites();

        Console.WriteLine("\nExtraction complete!");
    }
}

public class SpriteExtractor
{
    private readonly string _pokeemeraldPath;
    private readonly string _outputPath;
    private readonly string _spritesPath;
    private readonly string _palettesPath;

    public SpriteExtractor(string pokeemeraldPath, string outputPath)
    {
        _pokeemeraldPath = pokeemeraldPath;
        _outputPath = outputPath;
        _spritesPath = Path.Combine(pokeemeraldPath, "graphics/object_events/pics/people");
        _palettesPath = Path.Combine(pokeemeraldPath, "graphics/object_events/palettes");

        Directory.CreateDirectory(_outputPath);
    }

    public async Task ExtractAllSprites()
    {
        var spriteFiles = Directory.GetFiles(_spritesPath, "*.png", SearchOption.AllDirectories);
        Console.WriteLine($"Found {spriteFiles.Length} sprite files");

        int successCount = 0;

        foreach (var spriteFile in spriteFiles)
        {
            try
            {
                var manifest = await ExtractSprite(spriteFile);
                if (manifest != null)
                {
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {Path.GetFileName(spriteFile)}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nExtracted {successCount} sprites");
        Console.WriteLine($"Each sprite has its own manifest.json in its directory");
    }

    private async Task<SpriteManifest?> ExtractSprite(string spriteFilePath)
    {
        var relativePath = Path.GetRelativePath(_spritesPath, spriteFilePath);
        var spriteName = Path.GetFileNameWithoutExtension(spriteFilePath);
        var directory = Path.GetDirectoryName(relativePath) ?? "";

        // Determine sprite type and category
        // Player sprites (may, brendan) keep their original category name (may/brendan)
        // The directory name (may, brendan, generic, etc.) IS the category
        var isPlayerSprite = directory.StartsWith("may", StringComparison.OrdinalIgnoreCase) ||
                            directory.StartsWith("brendan", StringComparison.OrdinalIgnoreCase);
        var category = string.IsNullOrEmpty(directory) ? "generic" : directory.Split(Path.DirectorySeparatorChar)[0];

        Console.WriteLine($"Processing: {spriteName} ({category}) [Player: {isPlayerSprite}]");

        using var image = await Image.LoadAsync<Rgba32>(spriteFilePath);

        // Analyze sprite sheet to determine frame layout
        var frameInfo = AnalyzeSpriteSheet(image, spriteName);

        // Create output directory for this sprite
        // Player sprites go to Sprites/Players, others go to Sprites/NPCs
        var baseOutputPath = isPlayerSprite ? Path.Combine(_outputPath, "Players") : Path.Combine(_outputPath, "NPCs");
        var spriteOutputDir = Path.Combine(baseOutputPath, directory, spriteName);
        Directory.CreateDirectory(spriteOutputDir);

        // Create frame info with source rectangles (no individual PNGs)
        var frames = new List<FrameInfo>();
        for (int i = 0; i < frameInfo.FrameCount; i++)
        {
            frames.Add(new FrameInfo
            {
                Index = i,
                X = i * frameInfo.FrameWidth,
                Y = 0,
                Width = frameInfo.FrameWidth,
                Height = frameInfo.FrameHeight
            });
        }

        // Save sprite sheet with transparency
        var originalFileName = "spritesheet.png";
        var originalOutputPath = Path.Combine(spriteOutputDir, originalFileName);

        // Detect mask color FIRST (Pokemon Emerald uses magenta #FF00FF for transparency)
        var maskColor = DetectMaskColor(image);

        // Convert to RGBA32 to ensure true alpha transparency
        // (Pokemon Emerald uses indexed/palette PNGs with transparent color)
        using var rgbaImage = image.CloneAs<Rgba32>();

        // Apply transparency by replacing mask color with transparent pixels
        if (!string.IsNullOrEmpty(maskColor))
        {
            Console.WriteLine($"  Applying mask color {maskColor} for transparency");
            ApplyTransparency(rgbaImage, maskColor);
        }

        // Save as RGBA PNG with full transparency support
        var pngEncoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder
        {
            ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.RgbWithAlpha,
            BitDepth = SixLabors.ImageSharp.Formats.Png.PngBitDepth.Bit8,
            CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.DefaultCompression
        };
        await rgbaImage.SaveAsPngAsync(originalOutputPath, pngEncoder);

        // Create sprite manifest
        var manifest = new SpriteManifest
        {
            Name = spriteName,
            Category = category,
            OriginalPath = relativePath,
            OutputDirectory = Path.GetRelativePath(_outputPath, spriteOutputDir),
            SpriteSheet = originalFileName,
            FrameWidth = frameInfo.FrameWidth,
            FrameHeight = frameInfo.FrameHeight,
            FrameCount = frameInfo.FrameCount,
            Frames = frames,
            Animations = GenerateDefaultAnimations(frameInfo)
        };

        // Write individual manifest
        var manifestPath = Path.Combine(spriteOutputDir, "manifest.json");
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never  // Always write all fields
        });
        await File.WriteAllTextAsync(manifestPath, json);

        return manifest;
    }

    private SpriteSheetInfo AnalyzeSpriteSheet(Image<Rgba32> image, string spriteName)
    {
        // Pokemon Emerald sprites are typically organized as:
        // - 16x16 tiles (for small NPCs)
        // - 16x32 tiles (for normal NPCs) - most common
        // - 32x32 tiles (for larger NPCs or special sprites)

        // Common pattern: 9 frames for standard NPCs (3 facing down, 3 facing up, 3 facing side)
        // Or 18 frames for running animations

        var width = image.Width;
        var height = image.Height;

        // Detect frame size based on sprite sheet dimensions
        int frameWidth, frameHeight, frameCount;

        // Check for common patterns
        if (height == 64 && width % 64 == 0)
        {
            // 32x32 sprites (player, bike, etc.)
            frameWidth = 32;
            frameHeight = 32;
            frameCount = width / 32;
        }
        else if (height == 32 && width % 16 == 0)
        {
            // 16x32 sprites (most NPCs)
            frameWidth = 16;
            frameHeight = 32;
            frameCount = width / 16;
        }
        else if (height == 32 && width % 32 == 0)
        {
            // 32x32 sprites stored in 32-height sheet
            frameWidth = 32;
            frameHeight = 32;
            frameCount = width / 32;
        }
        else if (height == 16 && width % 16 == 0)
        {
            // 16x16 sprites (small NPCs)
            frameWidth = 16;
            frameHeight = 16;
            frameCount = width / 16;
        }
        else
        {
            // Default: try to detect based on width
            frameWidth = height; // Assume square frames
            frameHeight = height;
            frameCount = width / frameWidth;
        }

        return new SpriteSheetInfo
        {
            FrameWidth = frameWidth,
            FrameHeight = frameHeight,
            FrameCount = frameCount
        };
    }


    private List<AnimationInfo> GenerateDefaultAnimations(SpriteSheetInfo info)
    {
        var animations = new List<AnimationInfo>();

        // Standard Pokemon pattern for 9-frame sprites:
        // Frame 0: Standing South, Frame 1: Standing North, Frame 2: Standing West/East
        // Frame 3: Walk South step 1, Frame 4: Walk South step 2
        // Frame 5: Walk North step 1, Frame 6: Walk North step 2
        // Frame 7: Walk West step 1, Frame 8: Walk West step 2
        if (info.FrameCount == 9)
        {
            // South (down) animations
            animations.Add(new AnimationInfo
            {
                Name = "idle_down",
                Loop = true,
                FrameIndices = new[] { 0 },
                FrameDuration = 0.5f
            });

            animations.Add(new AnimationInfo
            {
                Name = "walk_down",
                Loop = true,
                FrameIndices = new[] { 3, 0, 4, 0 },  // step1, standing, step2, standing
                FrameDuration = 0.15f
            });

            // North (up) animations
            animations.Add(new AnimationInfo
            {
                Name = "idle_up",
                Loop = true,
                FrameIndices = new[] { 1 },
                FrameDuration = 0.5f
            });

            animations.Add(new AnimationInfo
            {
                Name = "walk_up",
                Loop = true,
                FrameIndices = new[] { 5, 1, 6, 1 },  // step1, standing, step2, standing
                FrameDuration = 0.15f
            });

            // Left animations (Pokemon sprites face left by default)
            animations.Add(new AnimationInfo
            {
                Name = "idle_left",
                Loop = true,
                FrameIndices = new[] { 2 },
                FrameDuration = 0.5f,
                FlipHorizontal = false
            });

            animations.Add(new AnimationInfo
            {
                Name = "walk_left",
                Loop = true,
                FrameIndices = new[] { 7, 2, 8, 2 },  // step1, standing, step2, standing
                FrameDuration = 0.15f,
                FlipHorizontal = false
            });

            // Right animations (flip the left-facing sprites)
            animations.Add(new AnimationInfo
            {
                Name = "idle_right",
                Loop = true,
                FrameIndices = new[] { 2 },
                FrameDuration = 0.5f,
                FlipHorizontal = true
            });

            animations.Add(new AnimationInfo
            {
                Name = "walk_right",
                Loop = true,
                FrameIndices = new[] { 7, 2, 8, 2 },  // step1, standing, step2, standing
                FrameDuration = 0.15f,
                FlipHorizontal = true
            });
        }
        else if (info.FrameCount == 18)
        {
            // Walking frames (0-8) + Running frames (9-17)
            // Same layout as 9-frame but repeated for running

            // Walking animations
            animations.Add(new AnimationInfo
            {
                Name = "idle_down",
                Loop = true,
                FrameIndices = new[] { 0 },
                FrameDuration = 0.5f
            });

            animations.Add(new AnimationInfo
            {
                Name = "walk_down",
                Loop = true,
                FrameIndices = new[] { 3, 0, 4, 0 },
                FrameDuration = 0.15f
            });

            animations.Add(new AnimationInfo
            {
                Name = "idle_up",
                Loop = true,
                FrameIndices = new[] { 1 },
                FrameDuration = 0.5f
            });

            animations.Add(new AnimationInfo
            {
                Name = "walk_up",
                Loop = true,
                FrameIndices = new[] { 5, 1, 6, 1 },
                FrameDuration = 0.15f
            });

            // Left animations (Pokemon sprites face left by default)
            animations.Add(new AnimationInfo
            {
                Name = "idle_left",
                Loop = true,
                FrameIndices = new[] { 2 },
                FrameDuration = 0.5f,
                FlipHorizontal = false
            });

            animations.Add(new AnimationInfo
            {
                Name = "walk_left",
                Loop = true,
                FrameIndices = new[] { 7, 2, 8, 2 },
                FrameDuration = 0.15f,
                FlipHorizontal = false
            });

            // Right animations (flip the left-facing sprites)
            animations.Add(new AnimationInfo
            {
                Name = "idle_right",
                Loop = true,
                FrameIndices = new[] { 2 },
                FrameDuration = 0.5f,
                FlipHorizontal = true
            });

            animations.Add(new AnimationInfo
            {
                Name = "walk_right",
                Loop = true,
                FrameIndices = new[] { 7, 2, 8, 2 },
                FrameDuration = 0.15f,
                FlipHorizontal = true
            });

            // Running animations (frames 9-17, same pattern)
            animations.Add(new AnimationInfo
            {
                Name = "run_down",
                Loop = true,
                FrameIndices = new[] { 12, 9, 13, 9 },
                FrameDuration = 0.1f
            });

            animations.Add(new AnimationInfo
            {
                Name = "run_up",
                Loop = true,
                FrameIndices = new[] { 14, 10, 15, 10 },
                FrameDuration = 0.1f
            });

            // Left running (Pokemon sprites face left by default)
            animations.Add(new AnimationInfo
            {
                Name = "run_left",
                Loop = true,
                FrameIndices = new[] { 16, 11, 17, 11 },
                FrameDuration = 0.1f,
                FlipHorizontal = false
            });

            // Right running (flip the left-facing sprites)
            animations.Add(new AnimationInfo
            {
                Name = "run_right",
                Loop = true,
                FrameIndices = new[] { 16, 11, 17, 11 },
                FrameDuration = 0.1f,
                FlipHorizontal = true
            });
        }
        else
        {
            // Default: create a single animation with all frames
            var indices = Enumerable.Range(0, info.FrameCount).ToArray();
            animations.Add(new AnimationInfo
            {
                Name = "default",
                Loop = true,
                FrameIndices = indices,
                FrameDuration = 0.15f
            });
        }

        return animations;
    }

    /// <summary>
    /// Apply transparency by replacing mask color with transparent pixels
    /// </summary>
    private void ApplyTransparency(Image<Rgba32> image, string maskColorHex)
    {
        // Parse hex color
        maskColorHex = maskColorHex.TrimStart('#');
        byte r = Convert.ToByte(maskColorHex.Substring(0, 2), 16);
        byte g = Convert.ToByte(maskColorHex.Substring(2, 2), 16);
        byte b = Convert.ToByte(maskColorHex.Substring(4, 2), 16);

        int transparentCount = 0;
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    // Check if pixel matches mask color (RGB only, ignore alpha)
                    if (row[x].R == r && row[x].G == g && row[x].B == b)
                    {
                        row[x] = new Rgba32(0, 0, 0, 0); // Fully transparent
                        transparentCount++;
                    }
                }
            }
        });

        Console.WriteLine($"  Made {transparentCount} pixels transparent");
    }

    /// <summary>
    /// Detects the mask color used for transparency in Pokemon Emerald sprites.
    /// Pokemon sprites use various cyan/teal shades as background color for transparency.
    /// Returns the most common color in the image (usually the background).
    /// </summary>
    private string? DetectMaskColor(Image<Rgba32> image)
    {
        // Count pixel colors to find the most common (background) color
        var colorCounts = new Dictionary<Rgba32, int>();

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    if (colorCounts.ContainsKey(pixel))
                        colorCounts[pixel]++;
                    else
                        colorCounts[pixel] = 1;
                }
            }
        });

        // Find the most common color (likely background)
        var mostCommonColor = colorCounts.OrderByDescending(kvp => kvp.Value).First();

        // If the most common color appears in more than 50% of pixels, it's probably background
        var totalPixels = image.Width * image.Height;
        if (mostCommonColor.Value > totalPixels * 0.4) // More than 40% of pixels
        {
            var color = mostCommonColor.Key;
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        return null; // No obvious background color detected
    }
}

public class SpriteSheetInfo
{
    public int FrameWidth { get; set; }
    public int FrameHeight { get; set; }
    public int FrameCount { get; set; }
}

public class SpriteManifest
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string OriginalPath { get; set; } = "";
    public string OutputDirectory { get; set; } = "";
    public string SpriteSheet { get; set; } = "";
    public int FrameWidth { get; set; }
    public int FrameHeight { get; set; }
    public int FrameCount { get; set; }
    public List<FrameInfo> Frames { get; set; } = new();
    public List<AnimationInfo> Animations { get; set; } = new();
}

public class FrameInfo
{
    public int Index { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class AnimationInfo
{
    public string Name { get; set; } = "";
    public bool Loop { get; set; }
    public int[] FrameIndices { get; set; } = Array.Empty<int>();
    public float FrameDuration { get; set; }
    public bool FlipHorizontal { get; set; }
}
