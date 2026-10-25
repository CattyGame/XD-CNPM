using System.Text.Json;
using AURA.Analysis.Api.Upload;
using Xunit;

namespace AURA.Analysis.Api.Tests;

public sealed class DuplicateReferenceTests
{
    [Fact]
    public void Queued_accepted_item_keeps_the_existing_wire_shape()
    {
        var item = new AcceptedItem(Guid.NewGuid(), "synthetic.png", "QUEUED", null);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(item, UploadJson.Options));
        Assert.Equal(new[] { "analysisId", "fileName", "processingStatus" },
            json.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());
    }

    [Fact]
    public void Legacy_stored_duplicate_response_can_replay_without_rewriting_its_fields()
    {
        var batch = Guid.NewGuid();
        var legacy = JsonSerializer.Serialize(new
        {
            batchId = batch,
            items = new[] { new { analysisId = Guid.NewGuid(), fileName = "legacy.png", processingStatus = "DUPLICATE",
                error = new { code = "ERR_DUPLICATE_REQUEST", message = "legacy", retryable = false } } },
            createdAt = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc),
            statusUrl = $"/api/v1/batches/{batch:D}"
        }, UploadJson.Options);
        var stored = JsonSerializer.Deserialize<AcceptedResponse>(legacy, UploadJson.Options)!;
        Assert.Null(Assert.Single(stored.Items).ExistingAnalysisId);
        using var before = JsonDocument.Parse(legacy);
        using var after = JsonDocument.Parse(JsonSerializer.Serialize(stored, UploadJson.Options));
        Assert.True(JsonElement.DeepEquals(before.RootElement, after.RootElement));
    }

    [Fact]
    public void Duplicate_batch_projects_the_existing_analysis_id()
    {
        var original = Guid.NewGuid();
        var row = new BatchStatusRow(Guid.NewGuid(), "synthetic.png", "DUPLICATE", "NOT_REQUIRED", null,
            false, null, null, original);
        var result = BatchStatusProjection.Create(new(Guid.NewGuid(), DateTime.UtcNow, [row]));
        var item = Assert.Single(result.Items);
        Assert.Equal(original, item.ExistingAnalysisId);
        Assert.Equal("ERR_DUPLICATE_REQUEST", item.Error!.Code);
        Assert.Null(item.Job);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, UploadJson.Options));
        Assert.Equal(original, json.RootElement.GetProperty("items")[0].GetProperty("existingAnalysisId").GetGuid());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Corrupt_duplicate_reference_is_a_dependency_error(int mode)
    {
        var image = Guid.NewGuid();
        Guid? existing = mode switch { 0 => null, 1 => Guid.Empty, 2 => image, _ => Guid.NewGuid() };
        var row = new BatchStatusRow(image, "synthetic.png", mode == 3 ? "QUEUED" : "DUPLICATE", "NOT_REQUIRED",
            null, false, null, null, existing);
        var error = Assert.Throws<UploadFailure>(() => BatchStatusProjection.Create(new(Guid.NewGuid(), DateTime.UtcNow, [row])));
        Assert.Equal((503, "ERR_NETWORK"), (error.Status, error.Code));
    }
}
