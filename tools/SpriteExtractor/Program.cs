using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

        // Parse animation metadata from pokeemerald source
        var animationParser = new PokeemeraldAnimationParser(pokeemeraldPath);
        var animationData = animationParser.ParseAnimationData();
        var filenameMapping = animationParser.GetFilenameMapping();

        var picTablesPath = Path.Combine(pokeemeraldPath, "src/data/object_events/object_event_pic_tables.h");
        var graphicsPath = Path.Combine(pokeemeraldPath, "src/data/object_events/object_event_graphics.h");
        var picTableSources = animationParser.ParsePicTableSources(picTablesPath, graphicsPath);

        var extractor = new SpriteExtractor(pokeemeraldPath, spritesBasePath, animationData, filenameMapping, picTableSources);
        await extractor.ExtractAllSprites();

        Console.WriteLine("\nExtraction complete!");
    }
}

/// <summary>
/// Parses animation metadata from pokeemerald source code
/// </summary>
public class PokeemeraldAnimationParser
{
    private readonly string _pokeemeraldPath;
    private Dictionary<string, string> _filenameMapping = new();

    public PokeemeraldAnimationParser(string pokeemeraldPath)
    {
        _pokeemeraldPath = pokeemeraldPath;
    }

    public Dictionary<string, string> GetFilenameMapping() => _filenameMapping;

    public Dictionary<string, SpriteAnimationMetadata> ParseAnimationData()
    {
        Console.WriteLine("Parsing animation data from pokeemerald source...");

        var metadata = new Dictionary<string, SpriteAnimationMetadata>(StringComparer.OrdinalIgnoreCase);

        // Parse object_event_graphics_info.h for animation table assignments
        var graphicsInfoPath = Path.Combine(_pokeemeraldPath, "src/data/object_events/object_event_graphics_info.h");
        var graphicsPath = Path.Combine(_pokeemeraldPath, "src/data/object_events/object_event_graphics.h");
        var picTablesPath = Path.Combine(_pokeemeraldPath, "src/data/object_events/object_event_pic_tables.h");
        var animsPath = Path.Combine(_pokeemeraldPath, "src/data/object_events/object_event_anims.h");

        if (!File.Exists(graphicsInfoPath) || !File.Exists(picTablesPath) || !File.Exists(animsPath))
        {
            Console.WriteLine("Warning: Could not find pokeemerald source files. Using default animations.");
            return metadata;
        }

        // Parse filename -> sprite name mapping from object_event_graphics.h
        var filenameMapping = ParseFilenameMapping(graphicsPath);

        // Parse animation tables from object_event_anims.h
        var animationTables = ParseAnimationTables(animsPath);

        // Parse sprite graphics info
        var graphicsInfo = ParseGraphicsInfo(graphicsInfoPath);

        // Parse frame counts and mappings from pic tables
        var frameCounts = ParseFrameCounts(picTablesPath);
        var frameMappings = ParseFrameMappings(picTablesPath);

        // Combine the data
        foreach (var (spriteName, animTable) in graphicsInfo)
        {
            if (frameCounts.TryGetValue(spriteName, out var frameCount))
            {
                metadata[spriteName] = new SpriteAnimationMetadata
                {
                    SpriteName = spriteName,
                    AnimationTable = animTable,
                    LogicalFrameCount = frameCount,
                    PhysicalFrameMapping = frameMappings.ContainsKey(spriteName)
                        ? frameMappings[spriteName]
                        : null,
                    AnimationDefinitions = animationTables.ContainsKey(animTable)
                        ? animationTables[animTable]
                        : new List<AnimationDefinition>()
                };
            }
        }

        Console.WriteLine($"Parsed animation data for {metadata.Count} sprites");
        Console.WriteLine($"Parsed {filenameMapping.Count} filename->sprite mappings");

        // Store filename mapping for later use
        _filenameMapping = filenameMapping;

        return metadata;
    }

    private Dictionary<string, string> ParseFilenameMapping(string filePath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(filePath)) return result;

        var content = File.ReadAllText(filePath);

        // Match: const u32 gObjectEventPic_<SpriteName>[] = INCBIN_U32("graphics/object_events/pics/people/<path>/<filename>.4bpp");
        // Extract: <path>/<filename> -> <SpriteName>
        var regex = new Regex(@"gObjectEventPic_(\w+)\[\]\s*=\s*INCBIN_U32\(""graphics/object_events/pics/people/([^""]+)\.4bpp""\)");
        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            var spriteName = match.Groups[1].Value; // e.g. "BrendanNormal", "MayRunning"
            var spriteFilePath = match.Groups[2].Value;     // e.g. "brendan/walking", "may/running"

            // Extract just the filename without path
            var filename = Path.GetFileName(spriteFilePath); // e.g. "walking", "running"
            var directory = Path.GetDirectoryName(spriteFilePath)?.Replace("\\", "/"); // e.g. "brendan", "may"

            // Create key as "directory/filename" to match our extraction
            var key = $"{directory}/{filename}";
            result[key] = spriteName;

            Console.WriteLine($"  Filename mapping: {key} -> {spriteName}");
        }

        return result;
    }

    /// <summary>
    /// Parse which PNG files belong to which sPicTable
    /// Returns: sPicTable name -> list of (gObjectEventPic name, frame range)
    /// </summary>
    public Dictionary<string, List<SpriteSourceInfo>> ParsePicTableSources(string picTablesPath, string graphicsPath)
    {
        var result = new Dictionary<string, List<SpriteSourceInfo>>();
        if (!File.Exists(picTablesPath) || !File.Exists(graphicsPath)) return result;

        var picTablesContent = File.ReadAllText(picTablesPath);
        var graphicsContent = File.ReadAllText(graphicsPath);

        // First, build a map of gObjectEventPic names to file paths
        var picToFile = new Dictionary<string, string>();
        var fileRegex = new Regex(@"gObjectEventPic_(\w+)\[\]\s*=\s*INCBIN_U32\(""graphics/object_events/pics/people/([^""]+)\.4bpp""\)");
        foreach (Match match in fileRegex.Matches(graphicsContent))
        {
            picToFile[match.Groups[1].Value] = match.Groups[2].Value;
        }

        // Parse each sPicTable and find which gObjectEventPic_* it references
        var tableRegex = new Regex(@"sPicTable_(\w+)\[\]\s*=\s*\{([^}]+)\}", RegexOptions.Singleline);
        foreach (Match tableMatch in tableRegex.Matches(picTablesContent))
        {
            var tableName = tableMatch.Groups[1].Value;
            var tableContent = tableMatch.Groups[2].Value;

            var sources = new List<SpriteSourceInfo>();
            var seenPics = new Dictionary<string, SpriteSourceInfo>();

            // Find all gObjectEventPic references in this table
            var frameRegex = new Regex(@"(?:overworld_frame|obj_frame_tiles)\(gObjectEventPic_(\w+)");
            foreach (Match frameMatch in frameRegex.Matches(tableContent))
            {
                var picName = frameMatch.Groups[1].Value;

                if (!seenPics.ContainsKey(picName))
                {
                    var sourceInfo = new SpriteSourceInfo
                    {
                        PicName = picName,
                        FilePath = picToFile.ContainsKey(picName) ? picToFile[picName] : "",
                        StartFrame = seenPics.Values.Sum(s => s.FrameCount),
                        FrameCount = 0
                    };
                    seenPics[picName] = sourceInfo;
                    sources.Add(sourceInfo);
                }
                seenPics[picName].FrameCount++;
            }

            if (sources.Count > 0)
            {
                result[tableName] = sources;
            }
        }

        return result;
    }

    private Dictionary<string, string> ParseGraphicsInfo(string filePath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var content = File.ReadAllText(filePath);

        // Match: const struct ObjectEventGraphicsInfo gObjectEventGraphicsInfo_<Name> = { ... .anims = sAnimTable_<AnimTable>, ... }
        var regex = new Regex(@"gObjectEventGraphicsInfo_(\w+)\s*=\s*\{[^}]*\.anims\s*=\s*sAnimTable_(\w+)", RegexOptions.Singleline);
        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            var spriteName = match.Groups[1].Value;
            var animTable = match.Groups[2].Value;
            result[spriteName] = animTable;

        }

        return result;
    }

    private Dictionary<string, int> ParseFrameCounts(string filePath)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var content = File.ReadAllText(filePath);

        // Match: sPicTable_<Name>[] = { ... }
        var regex = new Regex(@"sPicTable_(\w+)\[\]\s*=\s*\{([^}]+)\}", RegexOptions.Singleline);
        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            var spriteName = match.Groups[1].Value;
            var tableContent = match.Groups[2].Value;

            // Count the number of overworld_frame or obj_frame_tiles entries
            var frameMatches = Regex.Matches(tableContent, @"overworld_frame|obj_frame_tiles");
            result[spriteName] = frameMatches.Count;
        }

        return result;
    }

    private Dictionary<string, List<int>> ParseFrameMappings(string filePath)
    {
        var result = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        var content = File.ReadAllText(filePath);

        // Match: sPicTable_<Name>[] = { ... }
        var regex = new Regex(@"sPicTable_(\w+)\[\]\s*=\s*\{([^}]+)\}", RegexOptions.Singleline);
        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            var spriteName = match.Groups[1].Value;
            var tableContent = match.Groups[2].Value;

            // Parse overworld_frame(..., N) to extract the physical frame index N
            var frameMapping = new List<int>();
            var frameRegex = new Regex(@"overworld_frame\([^,]+,\s*\d+,\s*\d+,\s*(\d+)\)");
            var frameMatches = frameRegex.Matches(tableContent);

            foreach (Match frameMatch in frameMatches)
            {
                frameMapping.Add(int.Parse(frameMatch.Groups[1].Value));
            }

            // Handle obj_frame_tiles (single frame sprites)
            if (frameMapping.Count == 0 && tableContent.Contains("obj_frame_tiles"))
            {
                frameMapping.Add(0); // Single frame, index 0
            }

            if (frameMapping.Count > 0)
            {
                result[spriteName] = frameMapping;
            }
        }

        return result;
    }

    private Dictionary<string, List<AnimationDefinition>> ParseAnimationTables(string filePath)
    {
        var result = new Dictionary<string, List<AnimationDefinition>>();
        var content = File.ReadAllText(filePath);

        // Parse individual animation sequences
        var animSequences = new Dictionary<string, List<AnimFrame>>();
        var seqRegex = new Regex(@"sAnim_(\w+)\[\]\s*=\s*\{([^}]+)\}", RegexOptions.Singleline);
        var seqMatches = seqRegex.Matches(content);

        foreach (Match match in seqMatches)
        {
            var animName = match.Groups[1].Value;
            var animContent = match.Groups[2].Value;
            var frames = new List<AnimFrame>();

            // Parse ANIMCMD_FRAME(frameIndex, duration, .hFlip = true/false)
            var frameRegex = new Regex(@"ANIMCMD_FRAME\((\d+),\s*(\d+)(?:,\s*\.hFlip\s*=\s*(TRUE|FALSE))?\)");
            var frameMatches = frameRegex.Matches(animContent);

            foreach (Match frameMatch in frameMatches)
            {
                frames.Add(new AnimFrame
                {
                    FrameIndex = int.Parse(frameMatch.Groups[1].Value),
                    Duration = int.Parse(frameMatch.Groups[2].Value),
                    FlipHorizontal = frameMatch.Groups[3].Value == "TRUE"
                });
            }

            if (frames.Count > 0)
            {
                animSequences[$"sAnim_{animName}"] = frames;
            }
        }

        // Parse animation tables that reference sequences
        var tableRegex = new Regex(@"sAnimTable_(\w+)\[\]\s*=\s*\{([^}]+)\}", RegexOptions.Singleline);
        var tableMatches = tableRegex.Matches(content);

        foreach (Match match in tableMatches)
        {
            var tableName = match.Groups[1].Value;
            var tableContent = match.Groups[2].Value;
            var animations = new List<AnimationDefinition>();

            // Parse [ANIM_*] = sAnim_<Name>,
            // Matches both ANIM_STD_* and other patterns like ANIM_TAKE_OUT_ROD_SOUTH, ANIM_FIELD_MOVE, etc.
            var entryRegex = new Regex(@"\[ANIM_(?:STD_)?([A-Z_]+)\]\s*=\s*sAnim_(\w+)");
            var entryMatches = entryRegex.Matches(tableContent);

            foreach (Match entryMatch in entryMatches)
            {
                var animType = entryMatch.Groups[1].Value;
                var animSeqName = $"sAnim_{entryMatch.Groups[2].Value}";

                if (animSequences.TryGetValue(animSeqName, out var frames))
                {
                    animations.Add(new AnimationDefinition
                    {
                        Name = ConvertAnimTypeName(animType),
                        Frames = frames
                    });
                }
            }

            if (animations.Count > 0)
            {
                result[tableName] = animations;
            }
        }

        return result;
    }

    private string ConvertAnimTypeName(string emeraldName)
    {
        // Convert pokeemerald's SCREAMING_SNAKE_CASE to lowercase_snake_case
        // Examples:
        //   FACE_SOUTH -> face_south
        //   GO_SOUTH -> go_south
        //   TAKE_OUT_ROD_SOUTH -> take_out_rod_south
        //   FIELD_MOVE -> field_move
        // This preserves the original semantic meaning without hardcoded mappings
        return emeraldName.ToLower();
    }
}

public class SpriteAnimationMetadata
{
    public string SpriteName { get; set; } = "";
    public string AnimationTable { get; set; } = "";
    public int LogicalFrameCount { get; set; }
    public List<int>? PhysicalFrameMapping { get; set; } // Maps logical frame index -> physical frame index
    public List<AnimationDefinition> AnimationDefinitions { get; set; } = new();
}

public class AnimationDefinition
{
    public string Name { get; set; } = "";
    public List<AnimFrame> Frames { get; set; } = new();
}

public class AnimFrame
{
    public int FrameIndex { get; set; }
    public int Duration { get; set; }
    public bool FlipHorizontal { get; set; }
}

public class SpriteSourceInfo
{
    public string PicName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int StartFrame { get; set; }
    public int FrameCount { get; set; }
}

public class SpriteExtractor
{
    private readonly string _pokeemeraldPath;
    private readonly string _outputPath;
    private readonly string _spritesPath;
    private readonly string _palettesPath;
    private readonly Dictionary<string, SpriteAnimationMetadata> _animationData;
    private readonly Dictionary<string, string> _filenameMapping;
    private readonly Dictionary<string, List<SpriteSourceInfo>> _picTableSources;

    public SpriteExtractor(string pokeemeraldPath, string outputPath,
        Dictionary<string, SpriteAnimationMetadata> animationData,
        Dictionary<string, string> filenameMapping,
        Dictionary<string, List<SpriteSourceInfo>> picTableSources)
    {
        _pokeemeraldPath = pokeemeraldPath;
        _outputPath = outputPath;
        _animationData = animationData;
        _filenameMapping = filenameMapping;
        _picTableSources = picTableSources;
        _spritesPath = Path.Combine(pokeemeraldPath, "graphics/object_events/pics/people");
        _palettesPath = Path.Combine(pokeemeraldPath, "graphics/object_events/palettes");

        Directory.CreateDirectory(_outputPath);
    }

    public async Task ExtractAllSprites()
    {
        Console.WriteLine($"Found {_picTableSources.Count} sPicTable definitions");

        int successCount = 0;
        var processedFiles = new HashSet<string>();

        // Extract sprites based on sPicTable definitions
        foreach (var (picTableName, sources) in _picTableSources)
        {
            try
            {
                var manifest = await ExtractSpriteFromPicTable(picTableName, sources);
                if (manifest != null)
                {
                    successCount++;
                    foreach (var source in sources)
                    {
                        processedFiles.Add(source.FilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {picTableName}: {ex.Message}");
            }
        }

        // Also extract any standalone sprites not in sPicTables
        var allPngFiles = Directory.GetFiles(_spritesPath, "*.png", SearchOption.AllDirectories);
        foreach (var pngFile in allPngFiles)
        {
            var relativePath = Path.GetRelativePath(_spritesPath, pngFile).Replace("\\", "/");
            var pathWithoutExt = Path.ChangeExtension(relativePath, null);

            if (!processedFiles.Contains(pathWithoutExt))
            {
                try
                {
                    var manifest = await ExtractStandalonePng(pngFile);
                    if (manifest != null)
                    {
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {Path.GetFileName(pngFile)}: {ex.Message}");
                }
            }
        }

        Console.WriteLine($"\nExtracted {successCount} sprites");
        Console.WriteLine($"Each sprite has its own manifest.json in its directory");
    }

    private async Task<SpriteManifest?> ExtractSpriteFromPicTable(string picTableName, List<SpriteSourceInfo> sources)
    {
        if (sources.Count == 0) return null;

        // Determine the category and base name from the first source file
        var firstFilePath = sources[0].FilePath;
        var directory = Path.GetDirectoryName(firstFilePath)?.Replace("\\", "/") ?? "";
        var isPlayerSprite = directory.StartsWith("may", StringComparison.OrdinalIgnoreCase) ||
                            directory.StartsWith("brendan", StringComparison.OrdinalIgnoreCase);
        var category = string.IsNullOrEmpty(directory) ? "generic" : directory.Split('/')[0];

        // Use picTableName as the sprite name (e.g. "BrendanNormal" -> "normal")
        var spriteName = ConvertPicTableNameToSpriteName(picTableName, category);

        Console.WriteLine($"Processing: {picTableName} -> {spriteName} ({category}) [Player: {isPlayerSprite}]");
        Console.WriteLine($"  Combining {sources.Count} source file(s): {string.Join(", ", sources.Select(s => Path.GetFileName(s.FilePath)))}");

        // Load all source PNGs
        var sourceImages = new List<Image<Rgba32>>();
        int totalWidth = 0;
        int maxHeight = 0;
        int totalPhysicalFrames = 0;

        foreach (var source in sources)
        {
            var pngPath = Path.Combine(_spritesPath, source.FilePath + ".png");
            if (!File.Exists(pngPath))
            {
                Console.WriteLine($"  WARNING: Source file not found: {pngPath}");
                continue;
            }

            var img = await Image.LoadAsync<Rgba32>(pngPath);
            sourceImages.Add(img);
            totalWidth += img.Width;
            maxHeight = Math.Max(maxHeight, img.Height);

            // Determine frame count for this source
            var srcFrameInfo = AnalyzeSpriteSheet(img, Path.GetFileNameWithoutExtension(source.FilePath));
            totalPhysicalFrames += srcFrameInfo.FrameCount;
        }

        if (sourceImages.Count == 0)
        {
            Console.WriteLine($"  ERROR: No valid source images found for {picTableName}");
            return null;
        }

        // Detect mask color from first source image (Pokemon Emerald uses magenta #FF00FF for transparency)
        var maskColor = DetectMaskColor(sourceImages[0]);

        // Combine images horizontally with transparency applied
        var combined = new Image<Rgba32>(totalWidth, maxHeight);
        int currentX = 0;
        foreach (var img in sourceImages)
        {
            // Convert to RGBA32 and apply transparency
            using var rgbaImg = img.CloneAs<Rgba32>();
            if (!string.IsNullOrEmpty(maskColor))
            {
                ApplyTransparency(rgbaImg, maskColor);
            }

            combined.Mutate(ctx => ctx.DrawImage(rgbaImg, new Point(currentX, 0), 1f));
            currentX += img.Width;
        }

        if (!string.IsNullOrEmpty(maskColor))
        {
            Console.WriteLine($"  Applied mask color {maskColor} for transparency");
        }

        // Determine frame layout from first image (assuming all have same dimensions)
        var frameInfo = AnalyzeSpriteSheet(sourceImages[0], Path.GetFileNameWithoutExtension(sources[0].FilePath));

        // Create output directory (Players/category/spriteName or NPCs/category/spriteName structure)
        // For player sprites: Players/brendan/normal, Players/may/surfing
        // For NPCs: NPCs/generic/boy1, NPCs/elite_four/sidney, NPCs/gym_leaders/brawly
        var baseFolder = isPlayerSprite ? "Players" : "NPCs";
        var spriteOutputDir = isPlayerSprite
            ? Path.Combine(_outputPath, baseFolder, category, spriteName)
            : Path.Combine(_outputPath, baseFolder, string.IsNullOrEmpty(directory) ? "generic" : directory, spriteName);
        Directory.CreateDirectory(spriteOutputDir);

        // Save combined spritesheet with transparency
        var outputPath = Path.Combine(spriteOutputDir, "spritesheet.png");
        var pngEncoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder
        {
            ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.RgbWithAlpha,
            BitDepth = SixLabors.ImageSharp.Formats.Png.PngBitDepth.Bit8,
            CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.DefaultCompression
        };
        await combined.SaveAsPngAsync(outputPath, pngEncoder);

        // Get physical frame mapping
        var physicalFrameMapping = GetPhysicalFrameMapping(picTableName);

        // Create frame info
        var frames = new List<FrameInfo>();
        if (physicalFrameMapping != null && physicalFrameMapping.Count > 0)
        {
            for (int logicalIndex = 0; logicalIndex < physicalFrameMapping.Count; logicalIndex++)
            {
                int physicalIndex = physicalFrameMapping[logicalIndex];
                frames.Add(new FrameInfo
                {
                    Index = logicalIndex,
                    X = physicalIndex * frameInfo.FrameWidth,
                    Y = 0,
                    Width = frameInfo.FrameWidth,
                    Height = frameInfo.FrameHeight
                });
            }
        }
        else
        {
            for (int i = 0; i < totalPhysicalFrames; i++)
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
        }

        var logicalFrameCount = frames.Count;
        var animations = GenerateAnimations(picTableName, directory, new SpriteSheetInfo
        {
            FrameWidth = frameInfo.FrameWidth,
            FrameHeight = frameInfo.FrameHeight,
            FrameCount = totalPhysicalFrames
        });

        // Create manifest
        var manifest = new SpriteManifest
        {
            Name = spriteName,
            Category = category,
            OriginalPath = string.Join(", ", sources.Select(s => s.FilePath)),
            OutputDirectory = Path.GetRelativePath(_outputPath, spriteOutputDir),
            SpriteSheet = "spritesheet.png",
            FrameWidth = frameInfo.FrameWidth,
            FrameHeight = frameInfo.FrameHeight,
            FrameCount = logicalFrameCount,
            Frames = frames,
            Animations = animations
        };

        var manifestPath = Path.Combine(spriteOutputDir, "manifest.json");
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json);

        Console.WriteLine($"  ✓ Extracted {spriteName}: {frames.Count} frames, {animations.Count} animations");

        // Cleanup
        foreach (var img in sourceImages)
        {
            img.Dispose();
        }
        combined.Dispose();

        return manifest;
    }

    private string ConvertPicTableNameToSpriteName(string picTableName, string category)
    {
        // Remove category prefix (e.g. "BrendanNormal" -> "Normal", "MayWatering" -> "Watering")
        var prefixes = new[] { "Brendan", "May", "RubySapphireBrendan", "RubySapphireMay" };
        foreach (var prefix in prefixes)
        {
            if (picTableName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var suffix = picTableName.Substring(prefix.Length);
                return string.IsNullOrEmpty(suffix) ? PascalToSnakeCase(picTableName) : suffix.ToLower();
            }
        }

        // Fallback: convert PascalCase to snake_case (e.g. "Boy1" -> "boy_1")
        return PascalToSnakeCase(picTableName);
    }

    private string PascalToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Insert underscore before capitals (except first char) and digits
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (i > 0 && (char.IsUpper(c) || (char.IsDigit(c) && i > 0 && !char.IsDigit(input[i - 1]))))
            {
                result.Append('_');
            }
            result.Append(char.ToLower(c));
        }
        return result.ToString();
    }

    private async Task<SpriteManifest?> ExtractStandalonePng(string spriteFilePath)
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
        // Structure: Players/category/spriteName or NPCs/category/spriteName
        var baseFolder = isPlayerSprite ? "Players" : "NPCs";
        var spriteOutputDir = Path.Combine(_outputPath, baseFolder, string.IsNullOrEmpty(directory) ? "generic" : directory, spriteName);
        Directory.CreateDirectory(spriteOutputDir);

        // Get physical frame mapping from pokeemerald data
        var physicalFrameMapping = GetPhysicalFrameMapping(spriteName);

        // Create frame info with source rectangles (no individual PNGs)
        // If we have a mapping, use it; otherwise default to sequential frames
        var frames = new List<FrameInfo>();
        if (physicalFrameMapping != null && physicalFrameMapping.Count > 0)
        {
            // Use the pokeemerald frame mapping
            for (int logicalIndex = 0; logicalIndex < physicalFrameMapping.Count; logicalIndex++)
            {
                int physicalIndex = physicalFrameMapping[logicalIndex];
                frames.Add(new FrameInfo
                {
                    Index = logicalIndex, // Logical frame index (used by animations)
                    X = physicalIndex * frameInfo.FrameWidth, // Physical position in PNG
                    Y = 0,
                    Width = frameInfo.FrameWidth,
                    Height = frameInfo.FrameHeight
                });
            }
        }
        else
        {
            // Default: 1:1 mapping
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

        // Get animation data for this sprite
        var animations = GenerateAnimations(spriteName, directory, frameInfo);

        // The frame count in the manifest should be the number of LOGICAL frames
        // (i.e., the number of frames defined in the Frames array, not the physical PNG frame count)
        var logicalFrameCount = frames.Count;

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
            FrameCount = logicalFrameCount,
            Frames = frames,
            Animations = animations
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


    private List<int>? GetPhysicalFrameMapping(string spriteName)
    {
        // Try to find the frame mapping from pokeemerald data
        var possibleNames = new List<string>
        {
            spriteName,
            $"May{ToPascalCase(spriteName)}",
            $"Brendan{ToPascalCase(spriteName)}",
            $"May{spriteName}",
            $"Brendan{spriteName}",
        };

        foreach (var name in possibleNames)
        {
            if (_animationData.TryGetValue(name, out var metadata) &&
                metadata.PhysicalFrameMapping != null &&
                metadata.PhysicalFrameMapping.Count > 0)
            {
                return metadata.PhysicalFrameMapping;
            }
        }

        return null;
    }

    private List<AnimationInfo> GenerateAnimations(string picTableOrSpriteName, string directory, SpriteSheetInfo info)
    {
        // For sPicTable-based sprites, picTableOrSpriteName is the sPicTable name (e.g. "BrendanNormal", "MayWatering")
        // For standalone sprites, it's the filename

        // Build possible sprite lookup names from pokeemerald
        var possibleNames = new List<string>
        {
            picTableOrSpriteName, // Try the picTable name directly
            StripCommonSuffixes(picTableOrSpriteName), // Try without suffix
            $"May{ToPascalCase(picTableOrSpriteName)}",
            $"Brendan{ToPascalCase(picTableOrSpriteName)}",
        };

        static string StripCommonSuffixes(string name)
        {
            // Graphics info names often don't have Normal/Running suffixes
            if (name.EndsWith("Normal")) return name.Substring(0, name.Length - 6);
            if (name.EndsWith("Running")) return name.Substring(0, name.Length - 7);
            return name;
        }

        SpriteAnimationMetadata? metadata = null;
        foreach (var name in possibleNames)
        {
            if (string.IsNullOrEmpty(name)) continue; // Skip empty names

            if (_animationData.TryGetValue(name, out metadata))
            {
                Console.WriteLine($"  Found animation data for {picTableOrSpriteName} as {name} ({metadata.AnimationTable})");
                break;
            }
        }

        if (metadata != null && metadata.AnimationDefinitions.Count > 0)
        {
            // Use the parsed animation data from pokeemerald - NO hardcoded filtering or renaming
            var animations = new List<AnimationInfo>();
            var maxValidFrameIndex = metadata.LogicalFrameCount - 1;

            foreach (var animDef in metadata.AnimationDefinitions)
            {
                // Convert from pokeemerald animation frames to our format
                var frameIndices = animDef.Frames.Select(f => f.FrameIndex).ToArray();

                // Check if ALL frame indices are valid for this sprite
                // (some sprites have fewer frames than the animation table expects)
                if (frameIndices.Any(idx => idx > maxValidFrameIndex))
                {
                    // Skip this animation - it references frames that don't exist for this sprite
                    continue;
                }

                // Check if any frame uses horizontal flip (for right-facing animations)
                var usesFlip = animDef.Frames.Any(f => f.FlipHorizontal);

                // Calculate average frame duration (pokeemerald uses variable units, we use seconds)
                // GBA runs at ~60 fps, so duration=8 means 8/60 seconds
                var avgDuration = animDef.Frames.Count > 0
                    ? (float)(animDef.Frames.Average(f => f.Duration) / 60.0)
                    : 0.15f;

                // Use the animation name exactly as defined in pokeemerald
                animations.Add(new AnimationInfo
                {
                    Name = animDef.Name,
                    Loop = true,
                    FrameIndices = frameIndices,
                    FrameDuration = avgDuration,
                    FlipHorizontal = usesFlip
                });
            }

            return animations;
        }

        // No animation data found in pokeemerald - return empty list
        // Don't generate fake/hardcoded animations
        Console.WriteLine($"  WARNING: No animation data found for {picTableOrSpriteName} in pokeemerald source");
        return new List<AnimationInfo>();
    }

    private string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var parts = input.Split('_');
        var result = string.Join("", parts.Select(p =>
            p.Length > 0 ? char.ToUpper(p[0]) + p.Substring(1) : p));

        return result;
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
