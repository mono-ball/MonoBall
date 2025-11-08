using System.IO.Compression;
using ZstdSharp;

Console.WriteLine("Testing Zstd decompression...");

// Test data: Base64-encoded Zstd-compressed tile data (9 tiles: 1-9)
string base64Data = "KLUv/SAkIQEAAQAAAAIAAAADAAAABAAAAAUAAAAGAAAABwAAAAgAAAAJAAAA";

// Decode from Base64
byte[] compressed = Convert.FromBase64String(base64Data);
Console.WriteLine($"Compressed size: {compressed.Length} bytes");

// Decompress using Zstd
using var decompressor = new Decompressor();
byte[] decompressed = decompressor.Unwrap(compressed).ToArray();
Console.WriteLine($"Decompressed size: {decompressed.Length} bytes");

// Convert to int array
int[] tiles = new int[decompressed.Length / 4];
for (int i = 0; i < tiles.Length; i++)
{
    tiles[i] = BitConverter.ToInt32(decompressed, i * 4);
}

Console.WriteLine("Tile data:");
for (int i = 0; i < tiles.Length; i++)
{
    Console.Write($"{tiles[i]} ");
    if ((i + 1) % 3 == 0) Console.WriteLine(); // 3x3 grid
}

// Verify expected values
bool success = true;
for (int i = 0; i < tiles.Length; i++)
{
    if (tiles[i] != i + 1)
    {
        Console.WriteLine($"ERROR: Expected {i + 1}, got {tiles[i]} at index {i}");
        success = false;
    }
}

if (success)
{
    Console.WriteLine("\n✓ Zstd decompression successful!");
}
else
{
    Console.WriteLine("\n✗ Zstd decompression failed!");
}
