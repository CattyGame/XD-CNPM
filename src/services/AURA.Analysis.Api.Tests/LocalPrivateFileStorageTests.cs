using AURA.Analysis.Api.Upload;
using Xunit;

namespace AURA.Analysis.Api.Tests;

public class LocalPrivateFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aura-test-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public async Task Put_Open_Delete_hoat_dong_va_ghi_de_cung_key_khong_loi()
    {
        var s = new LocalPrivateFileStorage(_root);
        await s.PutAsync("aura-originals", "t/b/a", new MemoryStream(new byte[] { 1, 2, 3 }), "image/png", default);
        await s.PutAsync("aura-originals", "t/b/a", new MemoryStream(new byte[] { 9, 9 }), "image/png", default);
        await using (var r = await s.OpenReadAsync("aura-originals", "t/b/a", default))
        {
            var ms = new MemoryStream(); await r.CopyToAsync(ms);
            Assert.Equal(2, ms.Length);
        }
        await s.DeleteAsync("aura-originals", "t/b/a", default);
        await Assert.ThrowsAsync<FileNotFoundException>(() => s.OpenReadAsync("aura-originals", "t/b/a", default));
        Assert.Equal(0, Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task Key_co_duong_dan_thoat_thu_muc_bi_tu_choi()
    {
        var s = new LocalPrivateFileStorage(_root);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            s.PutAsync("aura-originals", "../../evil", new MemoryStream(new byte[] { 1 }), "image/png", default));
    }
}
