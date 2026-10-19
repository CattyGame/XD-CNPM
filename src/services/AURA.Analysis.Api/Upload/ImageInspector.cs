using System.Buffers.Binary;
using System.Security.Cryptography;

namespace AURA.Analysis.Api.Upload;

public sealed record ImageInspection(string MimeType, int Width, int Height, long SizeBytes, string Sha256);

/// <summary>Vấn đề của một ảnh. Status là mã HTTP theo contract (400, 413, 415).</summary>
public sealed record ImageProblem(int Status, string Code, string Issue);

public sealed record InspectionOutcome(ImageInspection? Image, ImageProblem? Problem)
{
    public static InspectionOutcome Ok(ImageInspection i) => new(i, null);
    public static InspectionOutcome Fail(int status, string code, string issue) => new(null, new ImageProblem(status, code, issue));
}

/// <summary>
/// Kiểm tra JPEG/PNG theo header, giới hạn kích thước/pixel và checksum SHA-256.
/// Với PNG, kiểm tra CRC32 của mọi chunk và cấu trúc IEND; chưa giải mã JPEG/IDAT đầy đủ.
/// Vì vậy CRC hợp lệ không đồng nghĩa dữ liệu nén chắc chắn giải mã được.
/// </summary>
public static class ImageInspector
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static async Task<InspectionOutcome> InspectAsync(Stream stream, string? declaredContentType,
        UploadOptions options, CancellationToken ct = default)
    {
        if (!stream.CanSeek)
            throw new ArgumentException("Stream phải hỗ trợ seek.", nameof(stream));

        long size = stream.Length;
        if (size == 0) return InspectionOutcome.Fail(400, "ERR_VALIDATION", "empty");
        if (size > options.MaxImageBytes) return InspectionOutcome.Fail(413, "ERR_PAYLOAD_TOO_LARGE", "too_large");

        stream.Position = 0;
        var head = new byte[8];
        int read = await ReadUpToAsync(stream, head, ct);

        string? mime = null;
        if (read >= 8 && head.AsSpan(0, 8).SequenceEqual(PngSignature)) mime = "image/png";
        else if (read >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) mime = "image/jpeg";
        if (mime is null) return InspectionOutcome.Fail(415, "ERR_UNSUPPORTED_MEDIA_TYPE", "unsupported_type");

        // Content-Type khai báo: bỏ qua khi trống hoặc application/octet-stream (NiFi); khác với nội dung thật thì từ chối.
        if (!string.IsNullOrWhiteSpace(declaredContentType))
        {
            var declared = declaredContentType.Split(';')[0].Trim();
            if (!declared.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
                && !declared.Equals(mime, StringComparison.OrdinalIgnoreCase))
                return InspectionOutcome.Fail(415, "ERR_UNSUPPORTED_MEDIA_TYPE", "content_type_mismatch");
        }

        (int w, int h)? dims;
        try
        {
            dims = mime == "image/png"
                ? await ReadPngAsync(stream, size, ct)
                : await ReadJpegAsync(stream, size, ct);
        }
        catch (EndOfStreamException)
        {
            dims = null;
        }
        if (dims is null) return InspectionOutcome.Fail(400, "ERR_VALIDATION", "corrupt");

        var (width, height) = dims.Value;
        if (width <= 0 || height <= 0) return InspectionOutcome.Fail(400, "ERR_VALIDATION", "corrupt");
        if ((long)width * height > options.MaxImagePixels)
            return InspectionOutcome.Fail(413, "ERR_PAYLOAD_TOO_LARGE", "pixels_exceeded");

        stream.Position = 0;
        var hash = await SHA256.HashDataAsync(stream, ct);
        stream.Position = 0;
        return InspectionOutcome.Ok(new ImageInspection(mime, width, height, size, Convert.ToHexString(hash).ToLowerInvariant()));
    }

    private static async Task<int> ReadUpToAsync(Stream s, byte[] buf, CancellationToken ct)
    {
        int total = 0;
        while (total < buf.Length)
        {
            int n = await s.ReadAsync(buf.AsMemory(total), ct);
            if (n == 0) break;
            total += n;
        }
        return total;
    }

    // PNG: kiểm tra giới hạn chunk và CRC32 cho mọi chunk, bao gồm IHDR/IDAT/IEND.
    private static async Task<(int, int)?> ReadPngAsync(Stream s, long size, CancellationToken ct)
    {
        s.Position = 8;
        var hdr = new byte[8];
        var crcBytes = new byte[4];
        var buffer = new byte[8192];
        int width = 0, height = 0;
        bool first = true;
        bool foundIend = false;

        while (s.Position + 12 <= size)
        {
            await s.ReadExactlyAsync(hdr, ct);
            uint len = BinaryPrimitives.ReadUInt32BigEndian(hdr.AsSpan(0, 4));
            string type = System.Text.Encoding.ASCII.GetString(hdr, 4, 4);
            if (len > int.MaxValue || s.Position + (long)len + 4 > size) return null;
            if (first && (type != "IHDR" || len != 13)) return null;
            if (type == "IHDR" && !first) return null;
            if (type == "IEND" && len != 0) return null;

            uint crc = 0xFFFFFFFF;
            crc = UpdateCrc32(crc, hdr.AsSpan(4, 4));
            byte[]? ihdr = first ? new byte[13] : null;
            long remaining = len;
            int ihdrOffset = 0;
            while (remaining > 0)
            {
                int take = (int)Math.Min(buffer.Length, remaining);
                Memory<byte> target = buffer.AsMemory(0, take);
                await s.ReadExactlyAsync(target, ct);
                crc = UpdateCrc32(crc, buffer.AsSpan(0, take));
                if (ihdr is not null)
                {
                    buffer.AsSpan(0, take).CopyTo(ihdr.AsSpan(ihdrOffset));
                    ihdrOffset += take;
                }
                remaining -= take;
            }
            await s.ReadExactlyAsync(crcBytes, ct);
            uint expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(crcBytes);
            if (~crc != expectedCrc) return null;

            if (ihdr is not null)
            {
                width = (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(ihdr.AsSpan(0, 4)), int.MaxValue);
                height = (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(ihdr.AsSpan(4, 4)), int.MaxValue);
            }
            first = false;
            if (type == "IEND")
            {
                foundIend = true;
                break;
            }
        }

        if (first || !foundIend || s.Position != size) return null;
        return (width, height);
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

    // JPEG: duyệt marker đến SOFn để lấy kích thước; kiểm tra EOI (FF D9) ở cuối file.
    private static async Task<(int, int)?> ReadJpegAsync(Stream s, long size, CancellationToken ct)
    {
        // EOI trong 64 byte cuối (cho phép vài byte đệm sau EOI)
        long tailLen = Math.Min(64, size);
        s.Position = size - tailLen;
        var tail = new byte[tailLen];
        await s.ReadExactlyAsync(tail, ct);
        bool hasEoi = false;
        for (int i = 0; i + 1 < tail.Length; i++)
            if (tail[i] == 0xFF && tail[i + 1] == 0xD9) { hasEoi = true; break; }
        if (!hasEoi) return null;

        s.Position = 2;
        var b = new byte[1];
        var seg = new byte[7];
        while (s.Position < size)
        {
            await s.ReadExactlyAsync(b, ct);
            if (b[0] != 0xFF) return null;
            int m;
            do
            {
                if (s.Position >= size) return null;
                await s.ReadExactlyAsync(b, ct);
                m = b[0];
            } while (m == 0xFF);

            if (m == 0x01 || (m >= 0xD0 && m <= 0xD7)) continue;     // marker không có độ dài
            if (m == 0xD8 || m == 0xD9 || m == 0xDA || m == 0x00) return null; // hết/thiếu trước SOF

            bool isSof = m >= 0xC0 && m <= 0xCF && m != 0xC4 && m != 0xC8 && m != 0xCC;
            if (isSof)
            {
                await s.ReadExactlyAsync(seg, ct);   // length(2) precision(1) height(2) width(2)
                int height = BinaryPrimitives.ReadUInt16BigEndian(seg.AsSpan(3, 2));
                int width = BinaryPrimitives.ReadUInt16BigEndian(seg.AsSpan(5, 2));
                return (width, height);
            }
            var lenBuf = new byte[2];
            await s.ReadExactlyAsync(lenBuf, ct);
            int len = BinaryPrimitives.ReadUInt16BigEndian(lenBuf);
            if (len < 2 || s.Position + len - 2 > size) return null;
            s.Position += len - 2;
        }
        return null;
    }
}
