using System.Buffers.Binary;

namespace AURA.Analysis.Api.Tests;

internal static class TestImages
{
    public static byte[] Png(int width, int height, bool withIend = true, int extra = 0)
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), (uint)height);
        ihdr[8] = 8; ihdr[9] = 2;
        Chunk(ms, "IHDR", ihdr);
        Chunk(ms, "IDAT", new byte[20 + extra]);
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
        uint crc = 0xFFFFFFFF;
        foreach (byte b in System.Text.Encoding.ASCII.GetBytes(type)) crc = UpdateCrc32(crc, b);
        foreach (byte b in data) crc = UpdateCrc32(crc, b);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc ^ 0xFFFFFFFF);
        s.Write(crcBytes);
    }

    private static uint UpdateCrc32(uint crc, byte value)
    {
        crc ^= value;
        for (int i = 0; i < 8; i++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        return crc;
    }

    /// <param name="seed">Đổi nội dung để tạo checksum khác nhau.</param>
    public static byte[] Jpeg(int width, int height, bool withEoi = true, bool withExif = false, int seed = 0)
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0xFF, 0xD8 });
        if (withExif)
        {
            var exif = new byte[40];
            ms.Write(new byte[] { 0xFF, 0xE1, 0x00, (byte)(exif.Length + 2) });
            ms.Write(exif);
        }
        ms.Write(new byte[] { 0xFF, 0xC0, 0x00, 0x11, 0x08 });
        var wh = new byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(wh.AsSpan(0, 2), (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(wh.AsSpan(2, 2), (ushort)width);
        ms.Write(wh);
        ms.Write(new byte[] { 0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01 });
        ms.Write(new byte[] { 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00 });
        var data = new byte[200];
        new Random(seed).NextBytes(data);
        for (int i = 0; i < data.Length; i++) if (data[i] == 0xFF) data[i] = 0x7F;
        ms.Write(data);
        if (withEoi) ms.Write(new byte[] { 0xFF, 0xD9 });
        return ms.ToArray();
    }
}
