using AURA.Analysis.Api.Upload;
using Xunit;

namespace AURA.Analysis.Api.Tests;

public class FingerprintTests
{
    private static readonly Guid Patient = Guid.Parse("9b2e5c10-1111-4a2b-8c3d-000000000001");

    [Fact]
    public void Cung_dau_vao_cung_fingerprint_dang_hex_64()
    {
        var a = RequestFingerprint.Compute(Patient, "FUNDUS", "left", new[] { "aa", "bb" });
        var b = RequestFingerprint.Compute(Patient, "FUNDUS", "left", new[] { "aa", "bb" });
        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
        Assert.Equal(a.ToLowerInvariant(), a);
    }

    [Fact]
    public void Thu_tu_anh_benh_nhan_va_eyeSide_lam_doi_fingerprint()
    {
        var baseFp = RequestFingerprint.Compute(Patient, "FUNDUS", "left", new[] { "aa", "bb" });
        Assert.True(baseFp != RequestFingerprint.Compute(Patient, "FUNDUS", "left", new[] { "bb", "aa" }));
        Assert.True(baseFp != RequestFingerprint.Compute(Guid.NewGuid(), "FUNDUS", "left", new[] { "aa", "bb" }));
        Assert.True(baseFp != RequestFingerprint.Compute(Patient, "FUNDUS", "right", new[] { "aa", "bb" }));
        Assert.True(baseFp != RequestFingerprint.Compute(Patient, "FUNDUS", "left", new[] { "aa" }));
    }

    [Fact]
    public void DeterministicIds_on_dinh_khac_nhau_theo_dau_vao_va_la_uuid_v5()
    {
        var a = DeterministicIds.Create("analysis", "t|r|k", "0");
        Assert.Equal(a, DeterministicIds.Create("analysis", "t|r|k", "0"));
        Assert.True(a != DeterministicIds.Create("analysis", "t|r|k", "1"));
        Assert.True(a != DeterministicIds.Create("file", "t|r|k", "0"));
        Assert.Equal('5', a.ToString()[14]);
    }
}
