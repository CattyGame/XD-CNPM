using Aura.Worker.Analysis.Processing;
using Aura.Worker.Analysis.Services;
using Xunit;

namespace Aura.Worker.Analysis.Tests;

public class AiResultValidatorTests
{
    private AiAnalyzeResponse CreateValidResponse()
    {
        return new AiAnalyzeResponse
        {
            RequestId = Guid.NewGuid(),
            Timestamp = "2026-10-30T12:00:00Z",
            Status = "WARNING",
            IsMock = false,
            ModelVersion = "frangi-v0.2",
            ConfigVersion = "s3-frangi-1.0.0",
            ThresholdVersion = "frangi-threshold-0.02-v1",
            Limitations = "Segmentation research baseline",
            ProcessingTimeMs = 120,
            ImageInfo = new AiImageInfo
            {
                Width = 1024,
                Height = 768,
                Channels = 3,
                EyeSide = "left",
                Modality = "FUNDUS"
            },
            Metrics = new AiMetrics
            {
                VesselDensity = 0.08,
                TortuosityIndex = null,
                AvRatio = null,
                FractalDimension = null,
                BranchingPoints = null
            },
            RiskAvailable = false,
            RiskAssessment = null,
            Warnings = new List<AiWarning>
            {
                new() { Code = "RISK_UNAVAILABLE", Message = "Frangi provides vessel segmentation only." }
            },
            Segmentation = new AiSegmentation
            {
                MaskFormat = "png_base64",
                MaskBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jZfoAAAAASUVORK5CYII=",
                OverlayBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jZfoAAAAASUVORK5CYII="
            }
        };
    }

    [Fact]
    public void Validate_ValidFrangiSprint3Response_ReturnsValid()
    {
        var response = CreateValidResponse();

        var result = AiResultValidator.Validate(response);

        Assert.True(result.IsValid);
        Assert.False(result.IsMockHold);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Validate_MockDetectedInRealNamespace_ReturnsMockHold()
    {
        var response = CreateValidResponse();
        response.IsMock = true;

        var result = AiResultValidator.Validate(response);

        Assert.False(result.IsValid);
        Assert.True(result.IsMockHold);
        Assert.Equal("ERR_AI_MOCK_DETECTED", result.ErrorCode);
    }

    [Fact]
    public void Validate_InvalidGeometry_ReturnsError()
    {
        var response = CreateValidResponse();
        response.ImageInfo.Width = 0;

        var result = AiResultValidator.Validate(response);

        Assert.False(result.IsValid);
        Assert.Equal("ERR_AI_OUTPUT_INVALID", result.ErrorCode);
    }

    [Fact]
    public void Validate_RiskAvailableFalse_WithRiskAssessment_ReturnsError()
    {
        var response = CreateValidResponse();
        response.RiskAvailable = false;
        response.RiskAssessment = new AiRiskAssessment { RiskScore = 0.5, RiskLevel = "LOW" };

        var result = AiResultValidator.Validate(response);

        Assert.False(result.IsValid);
        Assert.Equal("ERR_AI_OUTPUT_INVALID", result.ErrorCode);
    }

    [Fact]
    public void Validate_MissingRiskUnavailableWarning_ReturnsError()
    {
        var response = CreateValidResponse();
        response.Warnings.Clear();

        var result = AiResultValidator.Validate(response);

        Assert.False(result.IsValid);
        Assert.Equal("ERR_AI_OUTPUT_INVALID", result.ErrorCode);
    }

    [Fact]
    public void Validate_InvalidVesselDensity_ReturnsError()
    {
        var response = CreateValidResponse();
        response.Metrics.VesselDensity = 1.5; // Invalid > 1.0

        var result = AiResultValidator.Validate(response);

        Assert.False(result.IsValid);
        Assert.Equal("ERR_AI_OUTPUT_INVALID", result.ErrorCode);
    }
}
