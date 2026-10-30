using Aura.Worker.Analysis.Services;

namespace Aura.Worker.Analysis.Processing;

public class ValidationOutcome
{
    public bool IsValid { get; set; }
    public bool IsMockHold { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public static class AiResultValidator
{
    public static ValidationOutcome Validate(AiAnalyzeResponse response)
    {
        // 1. Check is_mock in real namespace
        if (response.IsMock)
        {
            return new ValidationOutcome
            {
                IsValid = false,
                IsMockHold = true,
                ErrorCode = "ERR_AI_MOCK_DETECTED",
                ErrorMessage = "AI returned mock=true in real namespace; holding for investigation."
            };
        }

        // 2. Validate Image Info & Geometry
        if (response.ImageInfo == null || response.ImageInfo.Width <= 0 || response.ImageInfo.Height <= 0)
        {
            return new ValidationOutcome
            {
                IsValid = false,
                ErrorCode = "ERR_AI_OUTPUT_INVALID",
                ErrorMessage = "Invalid image geometry in AI response."
            };
        }

        // 3. Validate Segmentation outputs
        if (response.Segmentation == null ||
            string.IsNullOrWhiteSpace(response.Segmentation.MaskBase64) ||
            string.IsNullOrWhiteSpace(response.Segmentation.OverlayBase64))
        {
            return new ValidationOutcome
            {
                IsValid = false,
                ErrorCode = "ERR_AI_OUTPUT_INVALID",
                ErrorMessage = "Missing mask or overlay base64 in segmentation output."
            };
        }

        // 4. Validate Risk Invariants for Frangi v0.2
        if (!response.RiskAvailable)
        {
            if (response.RiskAssessment != null)
            {
                return new ValidationOutcome
                {
                    IsValid = false,
                    ErrorCode = "ERR_AI_OUTPUT_INVALID",
                    ErrorMessage = "Risk assessment must be null when risk_available is false."
                };
            }

            var hasWarning = response.Warnings.Any(w => w.Code == "RISK_UNAVAILABLE");
            if (!hasWarning)
            {
                return new ValidationOutcome
                {
                    IsValid = false,
                    ErrorCode = "ERR_AI_OUTPUT_INVALID",
                    ErrorMessage = "Expected RISK_UNAVAILABLE warning when risk_available is false."
                };
            }
        }

        // 5. Validate Metrics
        if (response.Metrics == null || response.Metrics.VesselDensity < 0.0 || response.Metrics.VesselDensity > 1.0)
        {
            return new ValidationOutcome
            {
                IsValid = false,
                ErrorCode = "ERR_AI_OUTPUT_INVALID",
                ErrorMessage = "Vessel density must be between 0.0 and 1.0."
            };
        }

        return new ValidationOutcome { IsValid = true };
    }
}
