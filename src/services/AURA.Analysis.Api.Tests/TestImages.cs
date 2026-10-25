using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Compression;
using SkiaSharp;

namespace AURA.Analysis.Api.Tests;

/// <summary>Synthetic, decodable fixtures; never patient images. Corrupt fixtures are named explicitly.</summary>
internal static class TestImages
{
    private static readonly ConcurrentDictionary<(int Width, int Height), byte[]> JpegCache = new();

    public static byte[] Png(int width, int height, bool withIend = true, int extra = 0)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            // RGB scanlines; the first byte is PNG filter 0. Bound memory to one row while making fixtures.
            var row = new byte[checked(1 + width * 3)];
            for (int y = 0; y < height; y++) zlib.Write(row);
        }
        return PngWithIdat(width, height, compressed.ToArray(), withIend, extra);
    }

    public static byte[] CrcValidUndecodablePng(int width = 100, int height = 100) =>
        PngWithIdat(width, height, new byte[20], true, 0);

    public static byte[] CrcValidShortScanlinesPng()
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(new byte[] { 0 }); // Valid zlib stream but not enough decoded scanlines for 100x100 RGB.
        return PngWithIdat(100, 100, compressed.ToArray(), true, 0);
    }

    private static byte[] PngWithIdat(int width, int height, byte[] idat, bool withIend, int extra)
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), (uint)height);
        ihdr[8] = 8; ihdr[9] = 2;
        Chunk(ms, "IHDR", ihdr);
        Chunk(ms, "IDAT", idat);
        // Unknown ancillary chunk, for size-limit tests without damaging IDAT.
        if (extra > 0) Chunk(ms, "npAD", new byte[extra]);
        if (withIend) Chunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len, (uint)data.Length);
        s.Write(len);
        s.Write(System.Text.Encoding.ASCII.GetBytes(type));
        s.Write(data);
        uint crc = UpdateCrc32(0xFFFFFFFF, System.Text.Encoding.ASCII.GetBytes(type));
        crc = UpdateCrc32(crc, data);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, ~crc);
        s.Write(crcBytes);
    }

    private static uint UpdateCrc32(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc;
    }

    public static byte[] Jpeg(int width, int height, bool withEoi = true, bool withExif = false, int seed = 0)
    {
        var encoded = JpegCache.GetOrAdd((width, height), dimensions =>
        {
            using var bitmap = new SKBitmap(new SKImageInfo(dimensions.Width, dimensions.Height, SKColorType.Rgba8888, SKAlphaType.Opaque));
            bitmap.Erase(SKColors.Gray);
            using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 75);
            return data.ToArray();
        });
        using var ms = new MemoryStream();
        ms.Write(encoded.AsSpan(0, 2)); // SOI
        // A standards-compliant COM segment gives distinct raw hashes without nondeterministic random entropy.
        var comment = System.Text.Encoding.ASCII.GetBytes("AURA synthetic fixture seed=" + seed);
        ms.Write(new byte[] { 0xFF, 0xFE, 0, checked((byte)(comment.Length + 2)) });
        ms.Write(comment);
        if (withExif)
        {
            byte[] exif = [0x45, 0x78, 0x69, 0x66, 0, 0, 0x49, 0x49, 0x2A, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0];
            ms.Write(new byte[] { 0xFF, 0xE1, 0, checked((byte)(exif.Length + 2)) });
            ms.Write(exif);
        }
        ms.Write(encoded.AsSpan(2, encoded.Length - (withEoi ? 2 : 4)));
        return ms.ToArray();
    }

    /// <summary>Old SOF/EOI validation accepted this, despite missing quantization/Huffman tables and entropy data.</summary>
    public static byte[] HeaderOnlyJpeg()
    {
        byte[] prefix = [0xFF, 0xD8, 0xFF, 0xC0, 0, 17, 8, 0, 100, 0, 100,
            3, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1];
        return prefix.Concat(new byte[] { 0xFF, 0xD9 }).ToArray();
    }
}
