"""
Real Frangi v0.2 retinal vessel segmentation service for AURA AI Core (Sprint 3).

Conforms strictly to:
- config/ai-frangi.yaml
- contracts/ai-openapi-1.2.yaml
- contracts/ai-request-1.2.schema.json & ai-response-1.2.schema.json
- Baseline algorithm frozen in src/ai-core/scripts/baseline_benchmark.py
"""

from __future__ import annotations

import base64
import io
import math
import os
import time
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

import numpy as np
import yaml
from PIL import Image, UnidentifiedImageError
from scipy.ndimage import binary_erosion, binary_fill_holes, convolve
from skimage.filters import frangi, gaussian

from ..schemas.analysis import (
    AnalysisRequest,
    AnalysisResponse,
    AnalysisWarning,
    ImageInfo,
    SegmentationResult,
    VesselMetrics,
)
from ..schemas.common import AnalysisMode, AnalysisStatus
from .mock_service import InvalidImagePayloadError


LAPLACIAN_KERNEL = np.array(
    [
        [0.0, 1.0, 0.0],
        [1.0, -4.0, 1.0],
        [0.0, 1.0, 0.0],
    ],
    dtype=np.float64,
)

SUPPORTED_FRANGI_FORMATS = ("PNG", "JPEG")


class FrangiConfigError(ValueError):
    """Raised when ai-frangi.yaml is missing or violates Sprint 3 baseline invariants."""


class LowQualityImageError(ValueError):
    """Raised when an input image fails pre-Frangi engineering quality thresholds."""

    def __init__(self, message: str, report: "FrangiQualityReport") -> None:
        super().__init__(message)
        self.report = report


@dataclass(frozen=True)
class FrangiQualityReport:
    """Measured pre-Frangi quality metrics over the estimated interior region."""
    width: int
    height: int
    estimated_region_pixels: int
    region_ratio: float
    luminance: float
    contrast: float
    sharpness: float
    is_acceptable: bool
    failed_checks: Tuple[str, ...]
    message: str

    def to_details_dict(self) -> Dict[str, Any]:
        return {
            "is_mock": False,
            "quality_assessment_performed": True,
            "width": self.width,
            "height": self.height,
            "estimated_region_pixels": self.estimated_region_pixels,
            "region_ratio": round(self.region_ratio, 6),
            "luminance": round(self.luminance, 4),
            "contrast": round(self.contrast, 4),
            "sharpness": round(self.sharpness, 4),
            "failed_checks": list(self.failed_checks),
        }


@dataclass(frozen=True)
class FrangiRuntimeConfig:
    """Validated runtime configuration mapped from config/ai-frangi.yaml."""
    engine: str = "frangi"
    allow_mock: bool = False
    model_version: str = "frangi-v0.2"
    config_version: str = "s3-frangi-1.0.0"
    threshold_version: str = "frangi-threshold-0.02-v1"
    risk_available: bool = False
    modality: str = "FUNDUS"
    color: str = "RGB"
    max_bytes: int = 10_485_760
    max_pixels: int = 16_000_000
    resize: bool = False
    crop: bool = False
    channel: str = "green"
    scale: float = 255.0
    gaussian_sigma: float = 1.0
    max_rgb_greater_than: int = 20
    fill_holes: bool = True
    erosion_kernel: Tuple[int, int] = (3, 3)
    erosion_iterations: int = 12
    sigmas: Tuple[int, ...] = (1, 2, 3, 5, 8)
    alpha: float = 0.5
    beta: float = 0.5
    gamma: Optional[float] = None
    black_ridges: bool = True
    boundary_mode: str = "reflect"
    outside_region: float = 0.0
    threshold: float = 0.02
    min_luminance: float = 15.0
    max_luminance: float = 240.0
    min_contrast: float = 8.0
    min_sharpness: float = 3.5
    min_estimated_region_ratio: float = 0.15

    def __post_init__(self) -> None:
        self.validate()

    def validate(self) -> None:
        if self.engine != "frangi":
            raise FrangiConfigError("Frangi runtime requires engine='frangi'.")
        if self.allow_mock is not False:
            raise FrangiConfigError("Frangi runtime requires allowMock=false.")
        if self.risk_available is not False:
            raise FrangiConfigError("Frangi runtime requires riskAvailable=false.")
        if self.resize or self.crop:
            raise FrangiConfigError("Frangi baseline forbids resize or crop; original geometry must be preserved.")
        if self.max_bytes <= 0 or self.max_pixels <= 0:
            raise FrangiConfigError("maxBytes and maxPixels must be positive.")
        if not (0.0 < self.threshold < 1.0):
            raise FrangiConfigError("threshold must be in (0, 1).")
        if not self.sigmas or any(s <= 0 for s in self.sigmas):
            raise FrangiConfigError("sigmas must be positive numbers.")
        if len(self.erosion_kernel) != 2 or any(k <= 0 for k in self.erosion_kernel):
            raise FrangiConfigError("erosionKernel must be a 2D positive shape.")
        if self.erosion_iterations < 0:
            raise FrangiConfigError("erosionIterations must be non-negative.")
        for name in (
            "min_luminance",
            "max_luminance",
            "min_contrast",
            "min_sharpness",
            "min_estimated_region_ratio",
        ):
            val = getattr(self, name)
            if isinstance(val, bool) or not isinstance(val, (int, float)) or not math.isfinite(val):
                raise FrangiConfigError(f"{name} must be a finite number.")
        if not (0.0 <= self.min_luminance <= self.max_luminance <= 255.0):
            raise FrangiConfigError("luminance bounds must satisfy 0 <= min <= max <= 255.")
        if not (0.0 <= self.min_estimated_region_ratio <= 1.0):
            raise FrangiConfigError("minEstimatedRegionRatio must be in [0, 1].")


def default_frangi_config_path() -> Path:
    env_path = os.getenv("AURA_FRANGI_CONFIG_PATH")
    if env_path and env_path.strip():
        return Path(env_path.strip())
    return Path(__file__).resolve().parents[2] / "config" / "ai-frangi.yaml"


def load_frangi_config(config_path: Optional[Path] = None) -> FrangiRuntimeConfig:
    path = config_path or default_frangi_config_path()
    if not path.is_file():
        raise FrangiConfigError(f"Frangi configuration file not found: {path}")

    try:
        raw = yaml.safe_load(path.read_text(encoding="utf-8"))
    except Exception as exc:
        raise FrangiConfigError(f"Failed to parse Frangi YAML config: {exc}") from exc

    if not isinstance(raw, dict):
        raise FrangiConfigError("Frangi configuration root must be a YAML mapping.")

    inp = raw.get("input") or {}
    alg = raw.get("algorithm") or {}
    est = alg.get("estimatedRegion") or {}
    fr = alg.get("frangi") or {}
    qual = raw.get("quality") or {}

    try:
        erosion_kernel_raw = est.get("erosionKernel", [3, 3])
        sigmas_raw = fr.get("sigmas", [1, 2, 3, 5, 8])
        cfg = FrangiRuntimeConfig(
            engine=str(raw.get("engine", "")),
            allow_mock=bool(raw.get("allowMock", True)),
            model_version=str(raw.get("modelVersion", "")),
            config_version=str(raw.get("configVersion", "")),
            threshold_version=str(raw.get("thresholdVersion", "")),
            risk_available=bool(raw.get("riskAvailable", True)),
            modality=str(inp.get("modality", "FUNDUS")),
            color=str(inp.get("color", "RGB")),
            max_bytes=int(inp.get("maxBytes", 10_485_760)),
            max_pixels=int(inp.get("maxPixels", 16_000_000)),
            resize=bool(inp.get("resize", False)),
            crop=bool(inp.get("crop", False)),
            channel=str(alg.get("channel", "green")),
            scale=float(alg.get("scale", 255.0)),
            gaussian_sigma=float(alg.get("gaussianSigma", 1.0)),
            max_rgb_greater_than=int(est.get("maxRgbGreaterThan", 20)),
            fill_holes=bool(est.get("fillHoles", True)),
            erosion_kernel=(int(erosion_kernel_raw[0]), int(erosion_kernel_raw[1])),
            erosion_iterations=int(est.get("erosionIterations", 12)),
            sigmas=tuple(int(s) for s in sigmas_raw),
            alpha=float(fr.get("alpha", 0.5)),
            beta=float(fr.get("beta", 0.5)),
            gamma=None if fr.get("gamma") is None else float(fr.get("gamma")),
            black_ridges=bool(fr.get("blackRidges", True)),
            boundary_mode=str(fr.get("mode", "reflect")),
            outside_region=float(alg.get("outsideRegion", 0.0)),
            threshold=float(alg.get("threshold", 0.02)),
            min_luminance=float(qual.get("minLuminance", 15.0)),
            max_luminance=float(qual.get("maxLuminance", 240.0)),
            min_contrast=float(qual.get("minContrast", 8.0)),
            min_sharpness=float(qual.get("minSharpness", 3.5)),
            min_estimated_region_ratio=float(qual.get("minEstimatedRegionRatio", 0.15)),
        )
    except FrangiConfigError:
        raise
    except Exception as exc:
        raise FrangiConfigError(f"Invalid Frangi configuration field types: {exc}") from exc

    return cfg


def decode_and_validate_fundus_image(
    base64_str: str,
    config: Optional[FrangiRuntimeConfig] = None,
) -> Tuple[np.ndarray, int, int]:
    """
    Decode raw Base64 image into an RGB uint8 numpy array of shape (H, W, 3).
    Enforces Sprint 3 constraints:
    - Raw Base64 only (rejects data: URLs)
    - Max 10 MiB (10,485,760 bytes) and max 16,000,000 pixels
    - Formats: PNG or JPEG only (does not silently accept WEBP)
    - Preserves original width and height without resizing or cropping
    """
    if config is None:
        config = FrangiRuntimeConfig()

    if not base64_str or not isinstance(base64_str, str) or not base64_str.strip():
        raise InvalidImagePayloadError("Chuỗi image_base64 rỗng hoặc chỉ chứa khoảng trắng.")

    cleaned = base64_str.strip()
    if cleaned.lower().startswith("data:") or "," in cleaned:
        raise InvalidImagePayloadError(
            "image_base64 phải là chuỗi Base64 trần, không chấp nhận tiền tố data URL."
        )

    # Fast length guard before base64 decoding
    max_b64_chars = ((config.max_bytes + 2) // 3) * 4 + 64
    if len(cleaned) > max_b64_chars:
        raise InvalidImagePayloadError(
            f"Chuỗi image_base64 vượt quá giới hạn dung lượng cho phép ({config.max_bytes} bytes)."
        )

    try:
        image_data = base64.b64decode(cleaned, validate=True)
    except Exception as exc:
        raise InvalidImagePayloadError(
            f"Chuỗi Base64 không hợp lệ hoặc bị hỏng mã hóa: {exc}"
        ) from exc

    if len(image_data) > config.max_bytes:
        raise InvalidImagePayloadError(
            f"Dung lượng ảnh ({len(image_data)} bytes) vượt quá giới hạn ({config.max_bytes} bytes)."
        )

    if len(image_data) < 12:
        raise InvalidImagePayloadError(
            "Dữ liệu giải mã quá ngắn (<12 bytes), không phải ảnh hợp lệ."
        )

    try:
        with Image.open(io.BytesIO(image_data)) as image:
            format_name = (image.format or "").upper()
            width, height = image.size

            if format_name not in SUPPORTED_FRANGI_FORMATS:
                raise InvalidImagePayloadError(
                    f"Định dạng '{format_name or 'UNKNOWN'}' không được hỗ trợ. "
                    f"Chỉ chấp nhận: {', '.join(SUPPORTED_FRANGI_FORMATS)}."
                )

            if width <= 0 or height <= 0:
                raise InvalidImagePayloadError(
                    f"Kích thước ảnh ({width}x{height}) không hợp lệ."
                )

            if width * height > config.max_pixels:
                raise InvalidImagePayloadError(
                    f"Độ phân giải ảnh ({width}x{height} = {width * height} pixels) "
                    f"vượt quá giới hạn ({config.max_pixels} pixels)."
                )

            image.verify()

        with Image.open(io.BytesIO(image_data)) as image:
            image.load()
            rgb_image = image.convert("RGB")
            rgb_array = np.asarray(rgb_image, dtype=np.uint8)
    except InvalidImagePayloadError:
        raise
    except (
        UnidentifiedImageError,
        Image.DecompressionBombError,
        OSError,
        SyntaxError,
        ValueError,
    ) as exc:
        raise InvalidImagePayloadError(
            f"Dữ liệu không phải ảnh hợp lệ hoặc bị cắt cụt/lỗi CRC: {exc}"
        ) from exc

    if rgb_array.ndim != 3 or rgb_array.shape[2] != 3:
        raise InvalidImagePayloadError("Ảnh sau giải mã không đạt cấu trúc 3 kênh RGB.")

    return rgb_array, width, height


def estimate_prediction_region(
    rgb: np.ndarray,
    config: Optional[FrangiRuntimeConfig] = None,
) -> np.ndarray:
    """
    Estimate the interior retina region matching baseline_benchmark.py and ai-frangi.yaml:
    1. max(R, G, B) > 20
    2. binary_fill_holes
    3. binary_erosion with 3x3 ones kernel for 12 iterations (border_value=0)
    """
    if config is None:
        config = FrangiRuntimeConfig()

    estimated_region = np.max(rgb, axis=2) > config.max_rgb_greater_than
    if config.fill_holes:
        estimated_region = binary_fill_holes(estimated_region)

    if config.erosion_iterations > 0:
        structure = np.ones(config.erosion_kernel, dtype=bool)
        interior = binary_erosion(
            estimated_region,
            structure=structure,
            iterations=config.erosion_iterations,
            border_value=0,
        )
    else:
        interior = estimated_region

    return np.asarray(interior, dtype=bool)


def evaluate_frangi_quality(
    rgb: np.ndarray,
    interior: Optional[np.ndarray] = None,
    config: Optional[FrangiRuntimeConfig] = None,
) -> FrangiQualityReport:
    """
    Compute exact pre-Frangi quality definitions from config/ai-frangi.yaml:
    - grayscale: float64 0.299*R + 0.587*G + 0.114*B on RGB 0..255
    - luminance: mean grayscale over estimatedRegion after fill and erosion
    - contrast: population standard deviation (ddof=0) grayscale over estimatedRegion
    - sharpness: population variance (ddof=0) of grayscale Laplacian
      kernel [[0,1,0],[1,-4,1],[0,1,0]], reflect boundary, sampled over estimatedRegion
    - regionRatio: estimatedRegionPixels / (width * height)
    - rejectIf: region empty OR regionRatio < 0.15 OR luminance < 15 OR luminance > 240
      OR contrast < 8 OR sharpness < 3.5 (equality to each threshold is accepted)
    """
    if config is None:
        config = FrangiRuntimeConfig()
    config.validate()

    if rgb.ndim != 3 or rgb.shape[2] != 3:
        raise ValueError("rgb must have shape (H, W, 3).")

    height, width = int(rgb.shape[0]), int(rgb.shape[1])
    total_pixels = width * height
    if total_pixels <= 0:
        raise ValueError("Image dimensions must be positive.")

    if interior is None:
        interior = estimate_prediction_region(rgb, config)
    else:
        interior = np.asarray(interior, dtype=bool)
        if interior.shape != (height, width):
            raise ValueError("interior mask shape must match rgb height and width.")

    region_pixels = int(np.count_nonzero(interior))
    region_ratio = float(region_pixels) / float(total_pixels)

    grayscale = (
        0.299 * rgb[:, :, 0].astype(np.float64)
        + 0.587 * rgb[:, :, 1].astype(np.float64)
        + 0.114 * rgb[:, :, 2].astype(np.float64)
    )

    if region_pixels == 0:
        luminance = 0.0
        contrast = 0.0
        sharpness = 0.0
    else:
        region_gray = grayscale[interior]
        luminance = float(np.mean(region_gray))
        contrast = float(np.std(region_gray, ddof=0))
        laplacian = convolve(grayscale, LAPLACIAN_KERNEL, mode="reflect")
        sharpness = float(np.var(laplacian[interior], ddof=0))

    failed: List[str] = []
    if region_pixels == 0:
        failed.append("EMPTY_ESTIMATED_REGION")
    if region_ratio < config.min_estimated_region_ratio:
        failed.append("LOW_REGION_RATIO")
    if region_pixels > 0:
        if luminance < config.min_luminance:
            failed.append("LOW_LUMINANCE")
        if luminance > config.max_luminance:
            failed.append("HIGH_LUMINANCE")
        if contrast < config.min_contrast:
            failed.append("LOW_CONTRAST")
        if sharpness < config.min_sharpness:
            failed.append("LOW_SHARPNESS")

    is_acceptable = len(failed) == 0
    if is_acceptable:
        message = "Chất lượng ảnh đạt ngưỡng kỹ thuật để phân vùng Frangi."
    else:
        message = (
            "Ảnh không đạt ngưỡng chất lượng kỹ thuật đầu vào "
            f"({', '.join(failed)}): "
            f"region_ratio={region_ratio:.4f}, luminance={luminance:.2f}, "
            f"contrast={contrast:.2f}, sharpness={sharpness:.2f}."
        )

    return FrangiQualityReport(
        width=width,
        height=height,
        estimated_region_pixels=region_pixels,
        region_ratio=region_ratio,
        luminance=luminance,
        contrast=contrast,
        sharpness=sharpness,
        is_acceptable=is_acceptable,
        failed_checks=tuple(failed),
        message=message,
    )


def run_frangi_segmentation(
    rgb: np.ndarray,
    interior: np.ndarray,
    config: Optional[FrangiRuntimeConfig] = None,
) -> Tuple[np.ndarray, np.ndarray]:
    """
    Execute frozen Frangi v0.2 vessel segmentation on original geometry:
    - Green channel / 255.0
    - Gaussian smoothing sigma=1.0, mode='nearest'
    - Frangi sigmas=[1,2,3,5,8], alpha=0.5, beta=0.5, gamma=None, black_ridges=True, mode='reflect'
    - Zero outside interior region
    - Per-image max normalization; all-zero response gives all-zero mask
    - Threshold >= 0.02
    Returns (vessel_mask_bool, normalized_response).
    """
    if config is None:
        config = FrangiRuntimeConfig()

    if not np.any(interior):
        zeros_resp = np.zeros(rgb.shape[:2], dtype=np.float64)
        zeros_mask = np.zeros(rgb.shape[:2], dtype=bool)
        return zeros_mask, zeros_resp

    green = rgb[:, :, 1].astype(np.float64) / float(config.scale)
    green_smoothed = gaussian(
        green,
        sigma=config.gaussian_sigma,
        preserve_range=True,
        mode="nearest",
    )

    response = frangi(
        green_smoothed,
        sigmas=list(config.sigmas),
        alpha=config.alpha,
        beta=config.beta,
        gamma=config.gamma,
        black_ridges=config.black_ridges,
        mode=config.boundary_mode,
    )

    if not np.all(np.isfinite(response)):
        raise RuntimeError("Frangi response contains non-finite values.")

    response[~interior] = float(config.outside_region)

    maximum = float(response.max())
    if maximum > 0.0:
        response = response / maximum
        vessel_mask = response >= config.threshold
    else:
        response = np.zeros_like(response, dtype=np.float64)
        vessel_mask = np.zeros(rgb.shape[:2], dtype=bool)

    return vessel_mask, response


def encode_mask_and_overlay_png(
    rgb: np.ndarray,
    vessel_mask: np.ndarray,
) -> Tuple[str, str]:
    """
    Encode binary mask (PNG mode 'L' with 0/255 values) and vessel overlay
    (PNG mode 'RGB' blending detected vessels onto original fundus image),
    strictly preserving original (width, height) geometry.
    """
    mask_uint8 = np.where(vessel_mask, 255, 0).astype(np.uint8)
    mask_img = Image.fromarray(mask_uint8, mode="L")
    mask_buf = io.BytesIO()
    mask_img.save(mask_buf, format="PNG", optimize=True)
    mask_b64 = base64.b64encode(mask_buf.getvalue()).decode("ascii")

    # Overlay: highlight vessel pixels in bright emerald green (0, 255, 128) over original RGB
    overlay_arr = rgb.copy()
    overlay_arr[vessel_mask] = np.array([0, 255, 128], dtype=np.uint8)
    overlay_img = Image.fromarray(overlay_arr, mode="RGB")
    overlay_buf = io.BytesIO()
    overlay_img.save(overlay_buf, format="PNG", optimize=True)
    overlay_b64 = base64.b64encode(overlay_buf.getvalue()).decode("ascii")

    return mask_b64, overlay_b64


def utc_timestamp_z() -> str:
    """Return ISO-8601 UTC timestamp guaranteed to end with 'Z' per schema pattern 'Z$'."""
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def process_frangi_analysis(
    request: AnalysisRequest,
    config: Optional[FrangiRuntimeConfig] = None,
) -> AnalysisResponse:
    """
    End-to-end real Frangi v0.2 inference pipeline for POST /api/v1/analyze (Sprint 3 Contract 1.2.0).
    """
    start_time = time.perf_counter()

    if config is None:
        config = load_frangi_config()
    config.validate()

    # Validate request contract 1.2 invariants
    try:
        uuid.UUID(str(request.request_id))
    except ValueError as exc:
        raise ValueError("request_id must be a canonical UUID.") from exc

    if request.patient_id is not None:
        raise ValueError("patient_id must be null in AI request contract 1.2.")

    if request.mode != AnalysisMode.RETINA_VESSELS:
        raise ValueError("mode must be 'retina_vessels'.")

    if not request.include_mask or not request.include_overlay:
        raise ValueError("include_mask and include_overlay must both be true in Sprint 3 contract 1.2.")

    # Step 1: Decode and validate image bytes & dimensions without resizing/cropping
    rgb, width, height = decode_and_validate_fundus_image(request.image_base64, config=config)

    # Step 2: Estimate prediction region and evaluate pre-Frangi quality metrics
    interior = estimate_prediction_region(rgb, config=config)
    quality_report = evaluate_frangi_quality(rgb, interior=interior, config=config)
    if not quality_report.is_acceptable:
        raise LowQualityImageError(quality_report.message, quality_report)

    # Step 3: Run frozen Frangi v0.2 vessel segmentation
    vessel_mask, _ = run_frangi_segmentation(rgb, interior=interior, config=config)

    # Step 4: Encode mask (PNG L 0/255) and overlay (PNG RGB) in original geometry
    mask_b64, overlay_b64 = encode_mask_and_overlay_png(rgb, vessel_mask)

    foreground_pixels = int(np.count_nonzero(vessel_mask))
    vessel_density = round(float(foreground_pixels) / float(width * height), 6)
    elapsed_ms = round((time.perf_counter() - start_time) * 1000.0, 2)

    return AnalysisResponse(
        request_id=str(request.request_id),
        patient_id=None,
        timestamp=utc_timestamp_z(),
        status=AnalysisStatus.WARNING,
        warnings=[
            AnalysisWarning(
                code="RISK_UNAVAILABLE",
                message="Frangi provides vessel segmentation only; clinical risk assessment is not available.",
            )
        ],
        is_mock=False,
        model_version=config.model_version,
        config_version=config.config_version,
        threshold_version=config.threshold_version,
        limitations="Segmentation research baseline; no clinical risk assessment.",
        processing_time_ms=elapsed_ms,
        image_info=ImageInfo(
            width=width,
            height=height,
            channels=3,
            eye_side=request.eye_side,
            modality="FUNDUS",
        ),
        metrics=VesselMetrics(
            vessel_density=vessel_density,
            tortuosity_index=None,
            av_ratio=None,
            fractal_dimension=None,
            branching_points=None,
        ),
        risk_available=False,
        risk_assessment=None,
        segmentation=SegmentationResult(
            mask_format="png_base64",
            mask_base64=mask_b64,
            overlay_base64=overlay_b64,
        ),
    )
