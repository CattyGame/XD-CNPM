using System.Security.Cryptography;
using AURA.Analysis.Api.Upload;
using Xunit;

namespace AURA.Analysis.Api.Tests;

public class ImageInspectorTests
{
    private static readonly UploadOptions Opt = new();

    private static Task<InspectionOutcome> Run(byte[] bytes, string? type = null, UploadOptions? o = null) =>
        ImageInspector.InspectAsync(new MemoryStream(bytes), type, o ?? Opt);

    [Fact]
    public async Task Png_hop_le_doc_dung_kich_thuoc_va_checksum()
    {
        var bytes = TestImages.Png(1200, 800);
        var r = await Run(bytes, "image/png");
        Assert.Null(r.Problem);
        Assert.Equal("image/png", r.Image!.MimeType);
        Assert.Equal(1200, r.Image.Width);
        Assert.Equal(800, r.Image.Height);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), r.Image.Sha256);
    }

    [Fact]
    public async Task Jpeg_hop_le_co_exif_doc_dung_kich_thuoc()
    {
        var r = await Run(TestImages.Jpeg(2000, 1500, withExif: true), "image/jpeg");
        Assert.Null(r.Problem);
        Assert.Equal("image/jpeg", r.Image!.MimeType);
        Assert.Equal(2000, r.Image.Width);
        Assert.Equal(1500, r.Image.Height);
    }

    [Fact]
    public async Task File_rong_bi_400()
    {
        var r = await Run(Array.Empty<byte>());
        Assert.Equal(400, r.Problem!.Status);
        Assert.Equal("empty", r.Problem.Issue);
    }

    [Fact]
    public async Task Qua_lon_bi_413()
    {
        var r = await Run(TestImages.Png(100, 100, extra: 5000), null, new UploadOptions { MaxImageBytes = 1000 });
        Assert.Equal(413, r.Problem!.Status);
        Assert.Equal("too_large", r.Problem.Issue);
    }

    [Fact]
    public async Task Qua_nhieu_pixel_bi_413()
    {
        var r = await Run(TestImages.Png(5000, 4000));   // 20 triệu pixel
        Assert.Equal(413, r.Problem!.Status);
        Assert.Equal("pixels_exceeded", r.Problem.Issue);
    }

    [Fact]
    public async Task Dung_dung_gioi_han_pixel_van_hop_le()
    {
        var r = await Run(TestImages.Png(4000, 4000));   // đúng 16.000.000
        Assert.Null(r.Problem);
    }

    [Fact]
    public async Task Khong_phai_jpeg_png_bi_415()
    {
        var gif = System.Text.Encoding.ASCII.GetBytes("GIF89a").Concat(new byte[50]).ToArray();
        var r = await Run(gif);
        Assert.Equal(415, r.Problem!.Status);
        Assert.Equal("unsupported_type", r.Problem.Issue);
    }

    [Fact]
    public async Task Png_cut_khong_co_IEND_bi_400_corrupt()
    {
        var r = await Run(TestImages.Png(100, 100, withIend: false));
        Assert.Equal(400, r.Problem!.Status);
        Assert.Equal("corrupt", r.Problem.Issue);
    }

    [Fact]
    public async Task Jpeg_cut_khong_co_EOI_bi_400_corrupt()
    {
        var r = await Run(TestImages.Jpeg(100, 100, withEoi: false));
        Assert.Equal(400, r.Problem!.Status);
        Assert.Equal("corrupt", r.Problem.Issue);
    }

    [Fact]
    public async Task Content_type_khai_bao_sai_bi_415_con_octet_stream_duoc_chap_nhan()
    {
        var png = TestImages.Png(100, 100);
        var wrong = await Run(png, "image/jpeg");
        Assert.Equal(415, wrong.Problem!.Status);
        Assert.Equal("content_type_mismatch", wrong.Problem.Issue);

        var octet = await Run(png, "application/octet-stream");
        Assert.Null(octet.Problem);

        var withCharset = await Run(png, "IMAGE/PNG; charset=binary");
        Assert.Null(withCharset.Problem);
    }

    [Fact]
    public async Task Dau_vao_giu_nguyen_vi_tri_stream_ve_0_sau_khi_kiem_tra()
    {
        var ms = new MemoryStream(TestImages.Png(10, 10));
        var r = await ImageInspector.InspectAsync(ms, null, Opt);
        Assert.Null(r.Problem);
        Assert.Equal(0L, ms.Position);
    }
}
