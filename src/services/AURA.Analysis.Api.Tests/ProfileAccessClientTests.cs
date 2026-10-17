using System.Net;
using System.Text;
using System.Text.Json;
using AURA.Analysis.Api.Upload;
using Xunit;

namespace AURA.Analysis.Api.Tests;

public class ProfileAccessClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, string, HttpResponseMessage> Respond = (_, _) => new(HttpStatusCode.OK);
        public string? LastBody; public HttpRequestMessage? LastRequest;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            LastRequest = r; LastBody = r.Content is null ? "" : await r.Content.ReadAsStringAsync(ct);
            return Respond(r, LastBody);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode s = HttpStatusCode.OK) =>
        new(s) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static (ProfileAccessClient, StubHandler) Make()
    {
        var h = new StubHandler();
        return (new ProfileAccessClient(new HttpClient(h) { BaseAddress = new Uri("http://profile.test") }), h);
    }

    private static PatientAccessRequest Req(Guid? clinic = null) =>
        new(Guid.NewGuid(), clinic, Guid.NewGuid(), "ANALYSIS_CREATE");

    [Fact]
    public async Task Gui_dung_duong_dan_va_ten_truong_cua_Profile()
    {
        var (c, h) = Make();
        h.Respond = (_, _) => Json("""{"allowed":false,"permissionVersion":1}""");
        var clinic = Guid.NewGuid(); var req = Req(clinic);
        await c.ResolveAsync(req, default);
        Assert.Equal("/internal/v1/profile/access/resolve", h.LastRequest!.RequestUri!.AbsolutePath);
        using var doc = JsonDocument.Parse(h.LastBody!);
        var r = doc.RootElement;
        Assert.Equal(req.RequesterId, r.GetProperty("actorAccountId").GetGuid());
        Assert.Equal(req.PatientId, r.GetProperty("patientId").GetGuid());
        Assert.Equal(clinic, r.GetProperty("clinicContextId").GetGuid());
        Assert.Equal("ANALYSIS_CREATE", r.GetProperty("action").GetString());
    }

    [Fact]
    public async Task Duoc_phep_anh_xa_tenant_va_billing_owner()
    {
        var (c, h) = Make();
        var t = Guid.NewGuid(); var o = Guid.NewGuid();
        h.Respond = (_, _) => Json($$"""{"allowed":true,"accountStatus":"ACTIVE","tenantId":"{{t}}","tenantKind":"PERSONAL","ownerPrincipalId":"{{o}}","billingOwner":{"type":"ACCOUNT","id":"{{o}}"},"permissionVersion":3}""");
        var d = await c.ResolveAsync(Req(), default);
        Assert.True(d.Allowed);
        Assert.Equal(t, d.TenantId);
        Assert.Equal("PERSONAL", d.TenantKind);
        Assert.Equal(o, d.OwnerPrincipalId);
        Assert.Equal($"ACCOUNT:{o}", d.BillingOwner);
        Assert.Equal(3, d.PermissionVersion);
    }

    [Fact]
    public async Task Bi_tu_choi_cac_truong_tenant_null_khong_gay_loi()
    {
        var (c, h) = Make();
        h.Respond = (_, _) => Json("""{"allowed":false,"accountStatus":"ACTIVE","patientId":null,"tenantId":null,"tenantKind":null,"ownerPrincipalId":null,"billingOwner":null,"permissionVersion":1}""");
        var d = await c.ResolveAsync(Req(), default);
        Assert.False(d.Allowed);
    }

    [Fact]
    public async Task Allowed_true_nhung_thieu_tenant_bi_coi_la_khong_co_quyen()
    {
        var (c, h) = Make();
        h.Respond = (_, _) => Json("""{"allowed":true,"tenantId":null,"permissionVersion":1}""");
        Assert.False((await c.ResolveAsync(Req(), default)).Allowed);
    }

    [Fact]
    public async Task Loi_HTTP_tro_thanh_503_ERR_NETWORK()
    {
        foreach (var status in new[] { HttpStatusCode.InternalServerError, HttpStatusCode.BadGateway, HttpStatusCode.Unauthorized })
        {
            var (c, h) = Make();
            h.Respond = (_, _) => new HttpResponseMessage(status);
            var ex = await Assert.ThrowsAsync<UploadFailure>(() => c.ResolveAsync(Req(), default));
            Assert.Equal(503, ex.Status);
            Assert.Equal("ERR_NETWORK", ex.Code);
            Assert.True(ex.Retryable);
        }
    }

    [Fact]
    public async Task Mat_ket_noi_tro_thanh_503()
    {
        var (c, h) = Make();
        h.Respond = (_, _) => throw new HttpRequestException("down");
        var ex = await Assert.ThrowsAsync<UploadFailure>(() => c.ResolveAsync(Req(), default));
        Assert.Equal(503, ex.Status);
    }
}
