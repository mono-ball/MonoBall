using ZstdSharp;

// Simple program to generate Zstd-compressed Base64 data for test maps
// Input: tile data as int array
// Output: Base64-encoded Zstd-compressed bytes

// Tile data: 9 tiles in row-major order (3x3 grid)
int[] tileData = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

// Convert to little-endian byte array (4 bytes per int)
byte[] bytes = new byte[tileData.Length * 4];
for (int i = 0; i < tileData.Length; i++)
{
    byte[] intBytes = BitConverter.GetBytes(tileData[i]);
    Array.Copy(intBytes, 0, bytes, i * 4, 4);
}

// Compress with Zstd
using var compressor = new Compressor();
byte[] compressed = compressor.Wrap(bytes).ToArray();

// Encode as Base64
string base64 = Convert.ToBase64String(compressed);

Console.WriteLine("Uncompressed size: " + bytes.Length + " bytes");
Console.WriteLine("Compressed size: " + compressed.Length + " bytes");
Console.WriteLine("Base64 output:");
Console.WriteLine(base64);
