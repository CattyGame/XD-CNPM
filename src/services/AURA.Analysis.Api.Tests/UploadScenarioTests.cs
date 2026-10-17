using AURA.Analysis.Api.Upload;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AURA.Analysis.Api.Tests;

/// <summary>Kịch bản nghiệm thu W2-M2-03: ảnh hỏng/quá lớn/OCT, payload trùng/khác, ownership, đồng thời, lỗi lưu trữ.</summary>
public class UploadScenarioTests : IDisposable
{
    private static readonly Guid RequesterA = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid RequesterB = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Patient = Guid.Parse("9b2e5c10-1111-4a2b-8c3d-000000000001");
    private static readonly Guid Key = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aura-scn-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class Rig
    {
        public readonly FakeAccess Access = new();
        public readonly FakeBilling Billing = new();
        public readonly FakeRepo Repo = new();
        public readonly IPrivateFileStorage Storage;
        public readonly UploadService Service;
        public Rig(IPrivateFileStorage storage, UploadOptions? o = null)
        {
            Storage = storage;
            Service = new UploadService(o ?? new UploadOptions(), Access, Billing, Storage, Repo, TimeProvider.System, NullLogger<UploadService>.Instance);
        }
    }

    private Rig DiskRig(UploadOptions? o = null) => new(new LocalPrivateFileStorage(_root), o);
    private int FilesOnDisk() => Directory.Exists(_root) ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Length : 0;

    private static MemFile Jpeg(string name, int seed) => new(name, TestImages.Jpeg(1000, 800, seed: seed), "image/jpeg");

    private static UploadCommand Cmd(Guid requester, IReadOnlyList<IUploadedFile> files, Guid? key = null,
        string? modality = null, string? eye = null, Guid? patient = null) =>
        new(requester, null, key ?? Key, null, (patient ?? Patient).ToString(), modality, eye, files);

    private static async Task<UploadFailure> Fails(Func<Task> act) => await Assert.ThrowsAsync<UploadFailure>(act);

    // ---- Ảnh hỏng, quá lớn, OCT: không để lại file, không reserve ----

    [Fact]
    public async Task Anh_hong_qua_lon_qua_pixel_sai_loai_va_OCT_deu_khong_luu_file_va_khong_reserve()
    {
        var rig = DiskRig(new UploadOptions { MaxImageBytes = 6000 });
        var truncated = new MemFile("cut.png", TestImages.Png(100, 100, withIend: false), "image/png");
        var tooBig = new MemFile("big.png", TestImages.Png(100, 100, extra: 9000), "image/png");
        var tooManyPixels = new MemFile("px.jpg", TestImages.Jpeg(5000, 4000), "image/jpeg");
        var gif = new MemFile("x.gif", System.Text.Encoding.ASCII.GetBytes("GIF89a").Concat(new byte[40]).ToArray());
        var rigPx = DiskRig();    // giới hạn mặc định cho ca pixel

        Assert.Equal(400, (await Fails(() => rig.Service.CreateAsync(Cmd(RequesterA, new[] { Jpeg("ok.jpg", 1), truncated }), default))).Status);
        Assert.Equal(413, (await Fails(() => rig.Service.CreateAsync(Cmd(RequesterA, new[] { tooBig }), default))).Status);
        Assert.Equal(413, (await Fails(() => rigPx.Service.CreateAsync(Cmd(RequesterA, new[] { tooManyPixels }), default))).Status);
        Assert.Equal(415, (await Fails(() => rig.Service.CreateAsync(Cmd(RequesterA, new IUploadedFile[] { gif }), default))).Status);
        Assert.Equal(400, (await Fails(() => rig.Service.CreateAsync(Cmd(RequesterA, new[] { Jpeg("ok.jpg", 1) }, modality: "OCT"), default))).Status);

        Assert.Equal(0, FilesOnDisk());
        Assert.Equal(0, rig.Billing.ReserveCalls + rigPx.Billing.ReserveCalls);
        Assert.Equal(0, rig.Repo.Commits.Count + rigPx.Repo.Commits.Count);
    }

    [Fact]
    public async Task Dung_100_anh_thanh_cong_101_anh_bi_400()
    {
        var rig = DiskRig();
        var hundred = Enumerable.Range(0, 100).Select(i => (IUploadedFile)Jpeg($"{i}.jpg", i + 1)).ToList();
        var r = await rig.Service.CreateAsync(Cmd(RequesterA, hundred), default);
        Assert.Equal(100, r.Response.Items.Count);
        Assert.Equal(100, rig.Billing.ReserveCalls);
        Assert.Equal(100, FilesOnDisk());

        var tooMany = hundred.Append(Jpeg("101.jpg", 999)).ToList();
        var f = await Fails(() => rig.Service.CreateAsync(Cmd(RequesterA, tooMany, key: Guid.NewGuid()), default));
        Assert.Equal("too_many", f.Details[0].Issue);
        Assert.Equal(100, FilesOnDisk());
    }

    // ---- Payload trùng / khác ----

    [Fact]
    public async Task Doi_ten_file_van_replay_nhung_doi_eyeSide_hoac_benh_nhan_hoac_anh_la_409()
    {
        var rig = DiskRig();
        var bytes = TestImages.Jpeg(1000, 800, seed: 4);
        await rig.Service.CreateAsync(Cmd(RequesterA, new[] { new MemFile("a.jpg", bytes, "image/jpeg") }, eye: "left"), default);

        var renamed = await rig.Service.CreateAsync(Cmd(RequesterA, new[] { new MemFile("doi-ten.jpg", bytes, "image/jpeg") }, eye: "left"), default);
        Assert.True(renamed.Replayed);

        var diffEye = await Fails(() => rig.Service.CreateAsync(Cmd(RequesterA, new[] { new MemFile("a.jpg", bytes, "image/jpeg") }, eye: "right"), default));
        Assert.Equal("ERR_IDEMPOTENCY_KEY_REUSED", diffEye.Code);
        var diffPatient = await Fails(() => rig.Service.CreateAsync(Cmd(RequesterA, new[] { new MemFile("a.jpg", bytes, "image/jpeg") }, eye: "left", patient: Guid.NewGuid()), default));
        Assert.Equal("ERR_IDEMPOTENCY_KEY_REUSED", diffPatient.Code);
        var diffImage = await Fails(() => rig.Service.CreateAsync(Cmd(RequesterA, new[] { Jpeg("a.jpg", 77) }, eye: "left"), default));
        Assert.Equal("ERR_IDEMPOTENCY_KEY_REUSED", diffImage.Code);

        Assert.Equal(1, rig.Repo.Commits.Count);
        Assert.Equal(1, rig.Billing.ReserveCalls);
        Assert.Equal(1, FilesOnDisk());
    }

    [Fact]
    public async Task Eyeside_va_modality_mac_dinh_giong_gia_tri_ro_rang_nen_replay()
    {
        var rig = DiskRig();
        var bytes = TestImages.Jpeg(1000, 800, seed: 5);
        await rig.Service.CreateAsync(Cmd(RequesterA, new[] { new MemFile("a.jpg", bytes) }), default);
        var again = await rig.Service.CreateAsync(Cmd(RequesterA, new[] { new MemFile("a.jpg", bytes) }, modality: "FUNDUS", eye: "unknown"), default);
        Assert.True(again.Replayed);
    }

    // ---- Ownership ----

    [Fact]
    public async Task Requester_khac_cung_key_khong_nhan_lai_du_lieu_cua_nguoi_truoc()
    {
        var rig = DiskRig();
        var files = new[] { Jpeg("a.jpg", 6) };
        var a = await rig.Service.CreateAsync(Cmd(RequesterA, files), default);
        var b = await rig.Service.CreateAsync(Cmd(RequesterB, files), default);
        Assert.False(b.Replayed);
        Assert.True(a.Response.BatchId != b.Response.BatchId);
        Assert.Equal(2, rig.Repo.Commits.Count);
    }

    [Fact]
    public async Task Mat_quyen_sau_khi_tao_thi_replay_cung_bi_404()
    {
        var rig = DiskRig();
        var files = new[] { Jpeg("a.jpg", 7) };
        await rig.Service.CreateAsync(Cmd(RequesterA, files), default);
        rig.Access.Allowed = false;
        var f = await Fails(() => rig.Service.CreateAsync(Cmd(RequesterA, files), default));
        Assert.Equal(404, f.Status);
    }

    // ---- Đồng thời ----

    [Fact]
    public async Task Hai_muoi_request_dong_thoi_cung_key_chi_mot_batch_mot_bo_reservation_va_file()
    {
        var rig = DiskRig();
        var files = new[] { Jpeg("a.jpg", 8), Jpeg("b.jpg", 9) };
        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            try { return (UploadResult?)await rig.Service.CreateAsync(Cmd(RequesterA, files), default); }
            catch (UploadFailure f) when (f.Code == "ERR_REQUEST_IN_PROGRESS") { return null; }
        }).ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, rig.Repo.Commits.Count);
        Assert.Equal(2, rig.Billing.ReserveCalls);                 // một bộ reservation cho 2 ảnh
        Assert.Equal(2, FilesOnDisk());
        var batchIds = results.Where(r => r is not null).Select(r => r!.Response.BatchId).Distinct().ToList();
        Assert.Equal(1, batchIds.Count);                           // mọi request thành công cùng một batch
    }

    // ---- Lỗi lưu trữ / persistence: không file mồ côi ----

    [Fact]
    public async Task Loi_commit_roi_gui_lai_khong_de_file_mo_coi_tren_dia()
    {
        var rig = DiskRig();
        rig.Repo.FailCommit = true;
        var files = new[] { Jpeg("a.jpg", 10), Jpeg("b.jpg", 11), Jpeg("c.jpg", 12) };
        Assert.Equal(503, (await Fails(() => rig.Service.CreateAsync(Cmd(RequesterA, files), default))).Status);
        Assert.Equal(3, FilesOnDisk());                            // giữ lại chờ reconciliation/retry

        rig.Repo.FailCommit = false;
        var r = await rig.Service.CreateAsync(Cmd(RequesterA, files), default);
        Assert.False(r.Replayed);
        Assert.Equal(3, FilesOnDisk());                            // ghi đè cùng key: vẫn đúng 3 file
        Assert.Equal(3, rig.Billing.ByKey.Count);                  // vẫn đúng 3 reservation
    }

    [Fact]
    public async Task Loi_luu_file_giua_chung_tren_dia_thi_don_sach_file_va_hoan_credit()
    {
        var inner = new LocalPrivateFileStorage(_root);
        var failing = new FailingStorage(inner, failOnPut: 3);
        var rig = new Rig(failing);
        var files = new[] { Jpeg("a.jpg", 13), Jpeg("b.jpg", 14), Jpeg("c.jpg", 15) };
        Assert.Equal(503, (await Fails(() => rig.Service.CreateAsync(Cmd(RequesterA, files), default))).Status);
        Assert.Equal(0, FilesOnDisk());
        Assert.Equal(3, rig.Billing.Released.Count);
        Assert.Equal(0, rig.Repo.Commits.Count);
    }

    private sealed class FailingStorage : IPrivateFileStorage
    {
        private readonly IPrivateFileStorage _inner; private readonly int _failOnPut; private int _puts;
        public FailingStorage(IPrivateFileStorage inner, int failOnPut) { _inner = inner; _failOnPut = failOnPut; }
        public Task PutAsync(string c, string k, Stream s, string t, CancellationToken ct) =>
            ++_puts == _failOnPut ? throw new IOException("disk full") : _inner.PutAsync(c, k, s, t, ct);
        public Task<Stream> OpenReadAsync(string c, string k, CancellationToken ct) => _inner.OpenReadAsync(c, k, ct);
        public Task DeleteAsync(string c, string k, CancellationToken ct) => _inner.DeleteAsync(c, k, ct);
        public Task<FileReadAccess> CreateReadAccessAsync(string c, string k, TimeSpan ttl, CancellationToken ct) => _inner.CreateReadAccessAsync(c, k, ttl, ct);
    }
}
