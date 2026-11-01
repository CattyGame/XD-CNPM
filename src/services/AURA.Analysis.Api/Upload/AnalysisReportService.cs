using System.Globalization;
using System.Text;
using AURA.Analysis.Api.Auth;
using Microsoft.AspNetCore.Http;
using Npgsql;
using QRCoder;
using SkiaSharp;

namespace AURA.Analysis.Api.Upload;

public sealed record ReportFormatRequest(string Format);
public sealed record ReportDownloadResponse(Guid ReportId, string Format, string DownloadUrl, DateTime ExpiresAt);
public sealed record DoctorReportReview(Guid DoctorId, bool IsApproved, string? CorrectedRiskLevel,
    string? Reason, string? Notes, DateTime CreatedAt);

/// <summary>Generates authorized, de-identified batch exports and single-case PDF reports in private Blob storage.</summary>
public sealed class AnalysisReportService(
    AnalysisQueryService analyses,
    BatchStatusService batches,
    IFileAccessRepository files,
    IPrivateFileStorage storage,
    StorageOptions storageOptions,
    NpgsqlDataSource dataSource,
    TimeProvider clock)
{
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);

    public async Task<ReportDownloadResponse> CreateAnalysisPdfAsync(Guid requesterId, Guid? clinicId, Guid analysisId,
        string? correlationId, CancellationToken ct)
    {
        var detail = await analyses.GetAsync(requesterId, clinicId, analysisId, ct, correlationId);
        if (detail.ProcessingStatus != "COMPLETED" || detail.Result is null)
            throw new UploadFailure(409, "ERR_INVALID_STATE", "Chỉ có thể xuất báo cáo khi phân tích đã hoàn tất.");

        var original = await files.FindAsync(detail.AnalysisId, detail.Image.FileId, ct)
            ?? throw UploadFailure.Dependency("Analysis image metadata");
        var overlayRef = detail.Result.Annotations.FirstOrDefault(x => x.Kind == "overlay")
            ?? throw new UploadFailure(409, "ERR_REPORT_SOURCE_MISSING", "Chưa có ảnh overlay để tạo báo cáo.");
        var overlay = await files.FindAsync(detail.AnalysisId, overlayRef.FileId, ct)
            ?? throw UploadFailure.Dependency("Analysis overlay metadata");
        var review = await ReadReviewAsync(detail.AnalysisId, original.TenantId, ct);

        await using var originalStream = await storage.OpenReadAsync(storageOptions.OriginalContainer, original.ObjectKey, ct);
        await using var overlayStream = await storage.OpenReadAsync(storageOptions.AnnotationContainer, overlay.ObjectKey, ct);
        using var originalBitmap = SKBitmap.Decode(originalStream);
        using var overlayBitmap = SKBitmap.Decode(overlayStream);
        if (originalBitmap is null || overlayBitmap is null)
            throw UploadFailure.Dependency("Report image decoder");

        var reportId = Guid.NewGuid();
        await using var pdf = new MemoryStream();
        RenderSinglePdf(pdf, reportId, detail, review, originalBitmap, overlayBitmap, clock.GetUtcNow());
        pdf.Position = 0;
        return await StoreAndSignAsync(reportId, "pdf", original.TenantId, detail.BatchId, detail.AnalysisId,
            "application/pdf", pdf, ct);
    }

    public async Task<ReportDownloadResponse> CreateBatchReportAsync(Guid requesterId, Guid? clinicId, Guid batchId,
        string format, string? correlationId, CancellationToken ct)
    {
        var status = await batches.GetAsync(requesterId, clinicId, batchId, ct, correlationId);
        var normalized = (format ?? "").Trim().ToLowerInvariant();
        if (normalized is not ("pdf" or "csv"))
            throw UploadFailure.Validation(new ErrorDetail("format", "must_be_pdf_or_csv"));

        byte[] bytes;
        var reportId = Guid.NewGuid();
        var tenantId = await ReadTenantIdAsync(batchId, ct);
        if (normalized == "csv")
            bytes = RenderBatchCsv(status, clock.GetUtcNow());
        else
            bytes = RenderBatchPdf(status, reportId, clock.GetUtcNow());

        await using var content = new MemoryStream(bytes, writable: false);
        return await StoreAndSignAsync(reportId, normalized, tenantId, batchId, batchId,
            normalized == "csv" ? "text/csv" : "application/pdf", content, ct);
    }

    private async Task<Guid> ReadTenantIdAsync(Guid batchId, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT tenant_id FROM analysis.batches WHERE id=$1;");
        command.Parameters.AddWithValue(batchId);
        var value = await command.ExecuteScalarAsync(ct);
        if (value is Guid tenant && tenant != Guid.Empty) return tenant;
        throw new UploadFailure(404, "ERR_NOT_FOUND", "Không tìm thấy tài nguyên.");
    }

    private async Task<DoctorReportReview?> ReadReviewAsync(Guid analysisId, Guid tenantId, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT doctor_id,is_approved,corrected_risk_level,reason,doctor_notes,created_at
            FROM analysis.doctor_reviews
            WHERE tenant_id=$1 AND image_id=$2
            ORDER BY version DESC LIMIT 1;
            """);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(analysisId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new(reader.GetGuid(0), reader.GetBoolean(1), reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetDateTime(5));
    }

    private async Task<ReportDownloadResponse> StoreAndSignAsync(Guid reportId, string format, Guid tenantId,
        Guid batchId, Guid resourceId, string contentType, Stream content, CancellationToken ct)
    {
        var key = $"{tenantId:D}/{batchId:D}/{resourceId:D}/{reportId:D}";
        await storage.PutAsync(storageOptions.ReportContainer, key, content, contentType, ct);
        try
        {
            var access = await storage.CreateReadAccessAsync(storageOptions.ReportContainer, key, LinkLifetime, ct);
            return new(reportId, format, access.Url.ToString(), access.ExpiresAt.UtcDateTime);
        }
        catch
        {
            try { await storage.DeleteAsync(storageOptions.ReportContainer, key, ct); }
            catch { /* A private orphan is preferable to hiding the signing failure. */ }
            throw;
        }
    }

    private static byte[] RenderBatchCsv(BatchStatusResponse report, DateTimeOffset generatedAt)
    {
        var suspicious = report.Items.Count(x => x.ProcessingStatus == "COMPLETED" && x.RiskLevel is "MODERATE" or "HIGH");
        var denominator = report.Items.Count(x => x.ProcessingStatus == "COMPLETED");
        var rate = denominator == 0 ? "0.00%" : (100d * suspicious / denominator).ToString("F2", CultureInfo.InvariantCulture) + "%";
        using var output = new MemoryStream();
        using (var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true))
        {
            writer.WriteLine("recordType,caseCode,processingStatus,reviewStatus,riskLevel,errorCode,totalImages,completed,failed,suspiciousRate,generatedAtUtc");
            writer.WriteLine(CsvRow("summary", "", report.BatchStatus, "", "", "", report.Summary.Total,
                report.Summary.Succeeded, report.Summary.Failed, rate, generatedAt.ToString("O", CultureInfo.InvariantCulture)));
            var index = 0;
            foreach (var item in report.Items)
            {
                index++;
                writer.WriteLine(CsvRow("case", $"CASE-{index:D4}", item.ProcessingStatus, item.ReviewStatus,
                    item.RiskLevel ?? "", item.Error?.Code ?? "", "", "", "", "", ""));
            }
            writer.Flush();
        }
        return output.ToArray();
    }

    private static string CsvRow(params object?[] values) => string.Join(',', values.Select(value =>
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }));

    private static void RenderSinglePdf(Stream output, Guid reportId, AnalysisDetailResponse detail,
        DoctorReportReview? review, SKBitmap original, SKBitmap overlay, DateTimeOffset generatedAt)
    {
        using var document = SKDocument.CreatePdf(output);
        var canvas = document.BeginPage(595, 842);
        DrawText(canvas, "AURA - Fundus Analysis Report", 36, 42, 22, bold: true);
        DrawText(canvas, $"Report: {reportId:D}", 36, 70);
        DrawText(canvas, $"Patient ID: {detail.PatientId:D}", 36, 96);
        DrawText(canvas, $"Analysis: {detail.AnalysisId:D}    Batch: {detail.BatchId:D}", 36, 120);
        DrawText(canvas, $"Generated (UTC): {generatedAt:yyyy-MM-dd HH:mm:ss}", 36, 144);
        DrawText(canvas, $"AI result: {detail.Result!.RiskLevel ?? "Not classified"}    Review: {detail.ReviewStatus}", 36, 171, 13, true);
        DrawText(canvas, "Original fundus image", 36, 204, 12, true);
        DrawText(canvas, "Vessel overlay", 310, 204, 12, true);
        DrawImage(canvas, original, new SKRect(36, 214, 286, 405));
        DrawImage(canvas, overlay, new SKRect(310, 214, 560, 405));

        DrawText(canvas, "Vessel metrics", 36, 438, 14, true);
        var metrics = detail.Result.Metrics;
        var values = new (string Name, double Value)[]
        {
            ("Vessel density", metrics.VesselDensity ?? 0),
            ("Tortuosity", metrics.TortuosityIndex ?? 0),
            ("A/V ratio", metrics.AvRatio ?? 0),
            ("Fractal dimension", metrics.FractalDimension ?? 0),
            ("Branching points", metrics.BranchingPoints ?? 0)
        };
        DrawBars(canvas, values, 36, 454, 300);

        DrawText(canvas, "Doctor review", 36, 630, 14, true);
        if (review is null) DrawText(canvas, "No doctor review has been recorded.", 36, 654);
        else
        {
            DrawText(canvas, $"Doctor: {review.DoctorId:D}    Approved: {review.IsApproved}", 36, 654);
            DrawText(canvas, $"Corrected risk: {review.CorrectedRiskLevel ?? "None"}    Reason: {review.Reason ?? "None"}", 36, 678);
            DrawWrapped(canvas, review.Notes ?? "", 36, 702, 380, 16, 4);
        }
        DrawText(canvas, detail.Result.Disclaimer, 36, 798, 9);
        DrawQr(canvas, $"AURA-REPORT:{reportId:D}:ANALYSIS:{detail.AnalysisId:D}", 476, 700, 84);
        DrawText(canvas, "Reconciliation QR", 465, 796, 9);
        document.EndPage();
        document.Close();
    }

    private static byte[] RenderBatchPdf(BatchStatusResponse report, Guid reportId, DateTimeOffset generatedAt)
    {
        using var output = new MemoryStream();
        using var document = SKDocument.CreatePdf(output);
        var suspicious = report.Items.Count(x => x.ProcessingStatus == "COMPLETED" && x.RiskLevel is "MODERATE" or "HIGH");
        var denominator = report.Items.Count(x => x.ProcessingStatus == "COMPLETED");
        var rate = denominator == 0 ? 0 : (double)suspicious / denominator * 100;
        var page = document.BeginPage(595, 842);
        DrawText(page, "AURA - Batch Summary Report", 36, 42, 22, true);
        DrawText(page, $"Batch: {report.BatchId:D}    Report: {reportId:D}", 36, 72);
        DrawText(page, $"Generated (UTC): {generatedAt:yyyy-MM-dd HH:mm:ss}", 36, 96);
        DrawText(page, $"Status: {report.BatchStatus}    Total: {report.Summary.Total}    Completed: {report.Summary.Succeeded}    Failed: {report.Summary.Failed}", 36, 124);
        DrawText(page, $"Potentially atypical among completed cases: {rate:F2}% ({suspicious}/{denominator})", 36, 150, 12, true);
        DrawText(page, "Case (de-identified)", 36, 194, 10, true);
        DrawText(page, "Processing", 198, 194, 10, true);
        DrawText(page, "Review", 310, 194, 10, true);
        DrawText(page, "Risk", 410, 194, 10, true);
        DrawText(page, "Error", 490, 194, 10, true);
        var rowIndex = 0;
        foreach (var item in report.Items)
        {
            if (rowIndex > 0 && rowIndex % 28 == 0)
            {
                document.EndPage();
                page = document.BeginPage(595, 842);
                DrawText(page, "AURA - Batch Summary (continued)", 36, 42, 18, true);
                DrawText(page, "Case (de-identified)", 36, 72, 10, true);
                DrawText(page, "Processing", 198, 72, 10, true);
                DrawText(page, "Review", 310, 72, 10, true);
                DrawText(page, "Risk", 410, 72, 10, true);
                DrawText(page, "Error", 490, 72, 10, true);
            }
            var y = (rowIndex % 28) * 21 + (rowIndex < 28 ? 217 : 95);
            DrawText(page, $"CASE-{rowIndex + 1:D4}", 36, y, 9);
            DrawText(page, item.ProcessingStatus, 198, y, 9);
            DrawText(page, item.ReviewStatus, 310, y, 9);
            DrawText(page, item.RiskLevel ?? "-", 410, y, 9);
            DrawText(page, item.Error?.Code ?? "-", 490, y, 8);
            rowIndex++;
        }
        document.EndPage();
        document.Close();
        return output.ToArray();
    }

    private static void DrawText(SKCanvas canvas, string text, float x, float y, float size = 10, bool bold = false)
    {
        using var typeface = SKTypeface.FromFamilyName("Arial", bold ? SKFontStyle.Bold : SKFontStyle.Normal);
        using var font = new SKFont(typeface, size);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
    }

    private static void DrawImage(SKCanvas canvas, SKBitmap bitmap, SKRect bounds)
    {
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawBitmap(bitmap, bounds, new SKSamplingOptions(), paint);
    }

    private static void DrawBars(SKCanvas canvas, IReadOnlyList<(string Name, double Value)> values, float x, float y, float width)
    {
        for (var index = 0; index < values.Count; index++)
        {
            var rowY = y + index * 30;
            DrawText(canvas, values[index].Name, x, rowY + 12, 9);
            var value = double.IsFinite(values[index].Value) ? Math.Clamp(Math.Abs(values[index].Value), 0, 1) : 0;
            using var bar = new SKPaint { Color = new SKColor(45, 112, 164), IsAntialias = true };
            canvas.DrawRect(new SKRect(x + 116, rowY, x + 116 + (float)(value * width), rowY + 16), bar);
            DrawText(canvas, values[index].Value.ToString("F3", CultureInfo.InvariantCulture), x + 126 + (float)(value * width), rowY + 12, 8);
        }
    }

    private static void DrawWrapped(SKCanvas canvas, string text, float x, float y, float width, float lineHeight, int maxLines)
    {
        var words = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = new StringBuilder();
        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            using var typeface = SKTypeface.FromFamilyName("Arial");
            using var font = new SKFont(typeface, 9);
            if (font.MeasureText(candidate) > width && current.Length > 0)
            {
                lines.Add(current.ToString());
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }
        if (current.Length > 0) lines.Add(current.ToString());
        for (var i = 0; i < Math.Min(lines.Count, maxLines); i++)
            DrawText(canvas, lines[i], x, y + i * lineHeight, 9);
    }

    private static void DrawQr(SKCanvas canvas, string payload, float x, float y, float size)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var renderer = new PngByteQRCode(data);
        using var bitmap = SKBitmap.Decode(renderer.GetGraphic(6));
        if (bitmap is not null) DrawImage(canvas, bitmap, new SKRect(x, y, x + size, y + size));
    }
}
