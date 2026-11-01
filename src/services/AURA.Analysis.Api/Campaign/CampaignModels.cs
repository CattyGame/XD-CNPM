using System.Text.Json.Serialization;

namespace AURA.Analysis.Api.Campaign;

public static class CampaignStatus
{
    public const string Planned = "PLANNED";
    public const string InProgress = "IN_PROGRESS";
    public const string Completed = "COMPLETED";
    public const string Closed = "CLOSED";

    public static readonly HashSet<string> All = new(StringComparer.Ordinal)
    {
        Planned, InProgress, Completed, Closed
    };
}

public sealed record CreateCampaignRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("doctorInChargeId")] Guid? DoctorInChargeId,
    [property: JsonPropertyName("targetPatientCount")] int TargetPatientCount,
    [property: JsonPropertyName("startDate")] DateTime StartDate,
    [property: JsonPropertyName("endDate")] DateTime EndDate);

public sealed record UpdateCampaignStatusRequest(
    [property: JsonPropertyName("status")] string Status);

public sealed record LinkBatchToCampaignRequest(
    [property: JsonPropertyName("batchId")] Guid BatchId);

public sealed record CampaignResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("doctorInChargeId")] Guid? DoctorInChargeId,
    [property: JsonPropertyName("targetPatientCount")] int TargetPatientCount,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("startDate")] DateTime StartDate,
    [property: JsonPropertyName("endDate")] DateTime EndDate,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt);

public sealed record CampaignSummaryResponse(
    [property: JsonPropertyName("campaignId")] Guid CampaignId,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("campaignName")] string CampaignName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("targetPatientCount")] int TargetPatientCount,
    [property: JsonPropertyName("totalBatches")] int TotalBatches,
    [property: JsonPropertyName("totalImages")] int TotalImages,
    [property: JsonPropertyName("completedImages")] int CompletedImages,
    [property: JsonPropertyName("pendingImages")] int PendingImages,
    [property: JsonPropertyName("failedImages")] int FailedImages,
    [property: JsonPropertyName("completionRatePercent")] double CompletionRatePercent,
    [property: JsonPropertyName("normalCases")] int NormalCases,
    [property: JsonPropertyName("suspectedCases")] int SuspectedCases,
    [property: JsonPropertyName("highRiskCases")] int HighRiskCases,
    [property: JsonPropertyName("uniquePatients")] int UniquePatients);

public sealed record CampaignBatchItem(
    Guid BatchId,
    Guid PatientId,
    int TotalImages,
    int CompletedImages,
    int FailedImages,
    string Status,
    DateTime CreatedAt);
