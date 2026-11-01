using System.Text;
using AURA.Analysis.Api.Campaign;
using AURA.Analysis.Api.Upload;

namespace AURA.Analysis.Api.Http;

public static class CampaignEndpoints
{
    public static void MapCampaignEndpoints(this WebApplication app)
    {
        // 1. Tạo chiến dịch mới
        app.MapPost("/api/v1/campaigns", (HttpContext c, CreateCampaignRequest request) => Handle(c, async () =>
        {
            var tenantId = ResolveClinicTenantId(c);
            var repo = c.RequestServices.GetRequiredService<CampaignRepository>();
            var response = await repo.CreateCampaignAsync(tenantId, request, c.RequestAborted);
            return Results.Json(response, UploadJson.Options, statusCode: StatusCodes.Status201Created);
        }));

        // 2. Danh sách chiến dịch của phòng khám
        app.MapGet("/api/v1/campaigns", (HttpContext c, string? status) => Handle(c, async () =>
        {
            var tenantId = ResolveClinicTenantId(c);
            var repo = c.RequestServices.GetRequiredService<CampaignRepository>();
            var response = await repo.GetCampaignsAsync(tenantId, status, c.RequestAborted);
            return Results.Json(response, UploadJson.Options);
        }));

        // 3. Chi tiết chiến dịch
        app.MapGet("/api/v1/campaigns/{campaignId}", (HttpContext c, string campaignId) => Handle(c, async () =>
        {
            var tenantId = ResolveClinicTenantId(c);
            var campId = ParseGuid(campaignId, "campaignId");
            var repo = c.RequestServices.GetRequiredService<CampaignRepository>();
            var response = await repo.GetCampaignByIdAsync(tenantId, campId, c.RequestAborted);
            return Results.Json(response, UploadJson.Options);
        }));

        // 4. Cập nhật trạng thái chiến dịch
        app.MapPatch("/api/v1/campaigns/{campaignId}/status", (HttpContext c, string campaignId, UpdateCampaignStatusRequest request) => Handle(c, async () =>
        {
            var tenantId = ResolveClinicTenantId(c);
            var campId = ParseGuid(campaignId, "campaignId");
            var repo = c.RequestServices.GetRequiredService<CampaignRepository>();
            var response = await repo.UpdateCampaignStatusAsync(tenantId, campId, request.Status, c.RequestAborted);
            return Results.Json(response, UploadJson.Options);
        }));

        // 5. Gắn batch ảnh vào chiến dịch (AC1 & AC3: chặn nếu CLOSED)
        app.MapPost("/api/v1/campaigns/{campaignId}/batches", (HttpContext c, string campaignId, LinkBatchToCampaignRequest request) => Handle(c, async () =>
        {
            var tenantId = ResolveClinicTenantId(c);
            var campId = ParseGuid(campaignId, "campaignId");
            var repo = c.RequestServices.GetRequiredService<CampaignRepository>();
            await repo.LinkBatchAsync(tenantId, campId, request.BatchId, c.RequestAborted);
            return Results.Json(new { message = "Liên kết batch vào chiến dịch tầm soát thành công." }, UploadJson.Options);
        }));

        // 6. Tổng hợp thống kê tiến độ & lâm sàng (AC2)
        app.MapGet("/api/v1/campaigns/{campaignId}/summary", (HttpContext c, string campaignId) => Handle(c, async () =>
        {
            var tenantId = ResolveClinicTenantId(c);
            var campId = ParseGuid(campaignId, "campaignId");
            var repo = c.RequestServices.GetRequiredService<CampaignRepository>();
            var response = await repo.GetCampaignSummaryAsync(tenantId, campId, c.RequestAborted);
            return Results.Json(response, UploadJson.Options);
        }));

        // 7. Kết xuất dữ liệu báo cáo y tế CSV
        app.MapGet("/api/v1/campaigns/{campaignId}/export", (HttpContext c, string campaignId) => Handle(c, async () =>
        {
            var tenantId = ResolveClinicTenantId(c);
            var campId = ParseGuid(campaignId, "campaignId");
            var repo = c.RequestServices.GetRequiredService<CampaignRepository>();
            var csv = await repo.ExportCampaignReportCsvAsync(tenantId, campId, c.RequestAborted);

            c.Response.Headers.ContentDisposition = $"attachment; filename=screening_campaign_{campId:D}_summary.csv";
            return Results.Text(csv, "text/csv", Encoding.UTF8);
        }));
    }

    private static Guid ResolveClinicTenantId(HttpContext c)
    {
        // 1. Kiểm tra header X-Clinic-Context-Id
        if (c.Request.Headers.TryGetValue("X-Clinic-Context-Id", out var hVal)
            && Guid.TryParse(hVal.FirstOrDefault(), out var hId) && hId != Guid.Empty)
        {
            return hId;
        }

        // 2. Kiểm tra query param clinicContextId hoặc clinicId
        if (c.Request.Query.TryGetValue("clinicContextId", out var qVal)
            && Guid.TryParse(qVal.FirstOrDefault(), out var qId) && qId != Guid.Empty)
        {
            return qId;
        }
        if (c.Request.Query.TryGetValue("clinicId", out var qVal2)
            && Guid.TryParse(qVal2.FirstOrDefault(), out var qId2) && qId2 != Guid.Empty)
        {
            return qId2;
        }

        // Mặc định ném lỗi validation nếu thiếu thông tin Clinic Context
        throw UploadFailure.Validation(new ErrorDetail("clinicContextId", "Ngữ cảnh phòng khám (clinicContextId) là bắt buộc."));
    }

    private static Guid ParseGuid(string raw, string field)
    {
        return Guid.TryParse(raw, out var id) && id != Guid.Empty
            ? id
            : throw UploadFailure.Validation(new ErrorDetail(field, "Định dạng UUID không hợp lệ."));
    }

    private static async Task<IResult> Handle(HttpContext c, Func<Task<IResult>> action)
    {
        var header = c.Request.Headers["X-Correlation-Id"];
        var candidate = header.Count == 1 ? header[0] : null;
        var id = candidate is { Length: > 0 and <= 64 } && candidate.All(x => char.IsAsciiLetterOrDigit(x) || x is '.' or '_' or '-')
            ? candidate : Guid.NewGuid().ToString("D");
        c.Items["CorrelationId"] = id;
        c.Response.Headers["X-Correlation-Id"] = id;
        c.Response.Headers.CacheControl = "no-store";

        try
        {
            return await action();
        }
        catch (UploadFailure e)
        {
            return Results.Json(new
            {
                error = new
                {
                    code = e.Code,
                    message = e.Message,
                    correlationId = id,
                    retryable = e.Retryable,
                    details = e.Details
                }
            }, UploadJson.Options, statusCode: e.Status);
        }
        catch (OperationCanceledException) when (c.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Results.Json(new
            {
                error = new
                {
                    code = "ERR_NETWORK",
                    message = "Dịch vụ gặp sự cố kết nối cơ sở dữ liệu.",
                    correlationId = id,
                    retryable = true
                }
            }, UploadJson.Options, statusCode: 503);
        }
    }
}
