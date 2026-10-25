using System.Security.Cryptography;
using AURA.Analysis.Api.Upload;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AURA.Analysis.Api.Tests;

public sealed class ImageDecoderRegressionTests
{
    public static IEnumerable<object[]> BrokenImages()
    {
        yield return [TestImages.CrcValidUndecodablePng(), "image/png"];
        yield return [TestImages.CrcValidShortScanlinesPng(), "image/png"];
        yield return [TestImages.HeaderOnlyJpeg(), "image/jpeg"];
        var original = TestImages.Jpeg(1000, 800);
        int sos = original.AsSpan().IndexOf(new byte[] { 0xFF, 0xDA });
        if (sos < 0) throw new InvalidOperationException("Fixture must have an SOS marker.");
        // A decoder must inspect SOS/entropy, not return success merely from SOF dimensions + EOI.
        var invalidSelector = (byte[])original.Clone();
        invalidSelector[sos + 6] = 0xFF; // DC and AC Huffman table IDs 15 are invalid (legal IDs are 0..3).
        yield return [invalidSelector, "image/jpeg"];
    }

    [Theory]
    [MemberData(nameof(BrokenImages))]
    public async Task Header_CRC_or_EOI_is_insufficient_without_a_full_decode(byte[] bytes, string type)
    {
        using var stream = new MemoryStream(bytes);
        var result = await ImageInspector.InspectAsync(stream, type, new UploadOptions());
        Assert.Null(result.Image);
        Assert.Equal(400, result.Problem!.Status);
        Assert.Equal("ERR_VALIDATION", result.Problem.Code);
        Assert.Equal("corrupt", result.Problem.Issue);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [MemberData(nameof(BrokenImages))]
    public async Task Undecodable_images_stop_before_Profile_Billing_storage_or_commit(byte[] bytes, string type)
    {
        var access = new FakeAccess(); var billing = new FakeBilling();
        var storage = new FakeStorage(); var repository = new FakeRepo();
        var service = new UploadService(new UploadOptions(), access, billing, storage, repository,
            TimeProvider.System, NullLogger<UploadService>.Instance);
        var command = new UploadCommand(Guid.NewGuid(), null, Guid.NewGuid(), null,
            Guid.NewGuid().ToString(), null, null, [new MemFile("synthetic", bytes, type)]);
        var error = await Assert.ThrowsAsync<UploadFailure>(() => service.CreateAsync(command, default));
        Assert.Equal(400, error.Status);
        Assert.Equal("ERR_VALIDATION", error.Code);
        Assert.Equal(0, access.Calls);
        Assert.Equal(0, billing.ReserveCalls);
        Assert.Equal(0, storage.PutCalls);
        Assert.Empty(repository.Commits);
    }

    public static IEnumerable<object[]> IndependentImages()
    {
        yield return ["RGB PNG", "iVBORw0KGgoAAAANSUhEUgAAABgAAAASCAIAAADOjonJAAAAJElEQVR4nGNkYGDgZmCnHLEw8LMzMFABjRo0atCoQaMGDRaDAFajDgsWgEo1AAAAAElFTkSuQmCC", "image/png"];
        yield return ["Baseline JPEG", "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAUDBAQEAwUEBAQFBQUGBwwIBwcHBw8LCwkMEQ8SEhEPERETFhwXExQaFRERGCEYGh0dHx8fExciJCIeJBweHx7/2wBDAQUFBQcGBw4ICA4eFBEUHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh7/wAARCAASABgDASIAAhEBAxEB/8QAGAABAAMBAAAAAAAAAAAAAAAAAAUGBwj/xAAkEAABAgUEAgMAAAAAAAAAAAAAAQIFBiMyoQMEESISkRMhcf/EABgBAAMBAQAAAAAAAAAAAAAAAAUHCAIG/8QAJREAAAUDAQkAAAAAAAAAAAAAAAECBAYDBRFBByEiMTQ1cYGy/9oADAMBAAIRAxEAPwDn+DSnbSwXSDSnbSwaPBpTtpYLpBpTtpYNM3YERuZcuIZxBpTtpYBvMLldmlp/JqMRrWpyqqgCtWQMWWEOa6EGe/ClER49mHfbZepVAsHkRcGYz66N9F1gzGdejfQByrPQRbG9BPbprfHbt8U4VXKqcfgAEhtB77V8J+SFC2PpEj//2Q==", "image/jpeg"];
        yield return ["Progressive JPEG", "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAUDBAQEAwUEBAQFBQUGBwwIBwcHBw8LCwkMEQ8SEhEPERETFhwXExQaFRERGCEYGh0dHx8fExciJCIeJBweHx7/2wBDAQUFBQcGBw4ICA4eFBEUHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh7/wgARCAASABgDASIAAhEBAxEB/8QAGAABAAMBAAAAAAAAAAAAAAAAAAQFBgf/xAAYAQADAQEAAAAAAAAAAAAAAAAEBgcBBf/aAAwDAQACEAMQAAAB5/daO60PON4KeIt2cqLTwkUL/8QAGRAAAwADAAAAAAAAAAAAAAAAAAMEAQIR/9oACAEBAAEFAkyCZBMgmQTIKl5hIk2P/8QAGhEAAgIDAAAAAAAAAAAAAAAAAAUBBAIzYf/aAAgBAwEBPwFa56Vm8zgLSjqg/8QAHhEAAgIABwAAAAAAAAAAAAAAAAMBAgUGITRxgbH/2gAIAQIBAT8BS0tiCE6MvEcyJMwb63Xh/8QAGRABAQADAQAAAAAAAAAAAAAAADEBAhAR/9oACAEBAAY/AoiIj3PdX//EABkQAAIDAQAAAAAAAAAAAAAAAAABESExUf/aAAgBAQABPyHAYDAYDAIgISEXEIqpCqEjp//aAAwDAQACAAMAAAAQNOiB/8QAGREAAwADAAAAAAAAAAAAAAAAAAEREDFB/9oACAEDAQE/ENASY6cYX//EABsRAAIBBQAAAAAAAAAAAAAAAAABESFBgaGx/9oACAECAQE/EBiX91hCcZZYa/A//8QAGxAAAgMAAwAAAAAAAAAAAAAAAAERIfFxsdH/2gAIAQEAAT8QyTJMkyTJIMUJbaK/UedFAIbZqOD/2Q==", "image/jpeg"];
    }

    [Theory]
    [MemberData(nameof(IndependentImages))]
    public async Task Independent_Pillow_fixtures_decode_and_keep_original_bytes(string name, string base64, string type)
    {
        _ = name;
        var bytes = Convert.FromBase64String(base64);
        using var stream = new MemoryStream(bytes);
        stream.Position = 7;
        var result = await ImageInspector.InspectAsync(stream, type, new UploadOptions());
        Assert.Null(result.Problem);
        Assert.Equal(type, result.Image!.MimeType);
        Assert.Equal(24, result.Image.Width);
        Assert.Equal(18, result.Image.Height);
        Assert.Equal(bytes.Length, result.Image.SizeBytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), result.Image.Sha256);
        Assert.Equal(bytes, stream.ToArray());
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task Excessive_pixels_are_rejected_before_trying_the_corrupt_IDAT()
    {
        using var stream = new MemoryStream(TestImages.CrcValidUndecodablePng(5000, 4000));
        var result = await ImageInspector.InspectAsync(stream, "image/png", new UploadOptions());
        Assert.Equal(413, result.Problem!.Status);
        Assert.Equal("pixels_exceeded", result.Problem.Issue);
    }

    [Fact]
    public async Task Cancelled_caller_does_not_start_a_decode()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        using var stream = new MemoryStream(TestImages.Png(10, 10));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ImageInspector.InspectAsync(stream, "image/png", new UploadOptions(), cancelled.Token));
    }

    [Fact]
    public async Task Cancellation_during_snapshot_read_rewinds_without_closing_the_caller_stream()
    {
        using var cancelled = new CancellationTokenSource();
        using var stream = new CancellingStream(TestImages.Png(10, 10), cancelled);
        stream.Position = 3;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ImageInspector.InspectAsync(stream, "image/png", new UploadOptions(), cancelled.Token));
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(0, 16_000_000)]
    [InlineData(10_485_761, 16_000_000)]
    [InlineData(10_485_760, 0)]
    [InlineData(10_485_760, 16_000_001)]
    public async Task Invalid_configuration_cannot_raise_the_contract_ceilings(long bytes, long pixels)
    {
        using var stream = new MemoryStream(TestImages.Png(10, 10));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ImageInspector.InspectAsync(stream, null,
            new UploadOptions { MaxImageBytes = bytes, MaxImagePixels = pixels }));
    }

    private sealed class CancellingStream(byte[] bytes, CancellationTokenSource source) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            source.Cancel();
            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}
