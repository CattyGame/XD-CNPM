"""
AURA Retina Fundus Preprocessing Module.
Standardized preprocessing pipeline for Fundus photography:
- FOV (Field of View) detection & segmentation
- Centering, cropping, and square-aspect padding
- Green-channel extraction (highest hemoglobin contrast)
- Local contrast enhancement (Adaptive Contrast Stretching & Sharpness Enhancement)
- Resolution standardization (e.g. 512x512 with high-fidelity Lanczos resampling)
- Comprehensive image quality assessment (luminance, contrast, sharpness, FOV ratio)
"""

from __future__ import annotations

import base64
import io
import math
from dataclasses import dataclass, field
from enum import Enum
from typing import Optional, Tuple, Union

from PIL import Image, ImageChops, ImageEnhance, ImageFilter, ImageOps, ImageStat


class QualityRating(str, Enum):
    GOOD = "GOOD"
    ACCEPTABLE = "ACCEPTABLE"
    POOR = "POOR"
    INVALID = "INVALID"


@dataclass(frozen=True)
class QualityMetrics:
    mean_luminance: float
    contrast_score: float
    sharpness_score: float
    fov_ratio: float
    is_valid_fundus: bool
    quality_rating: QualityRating
    details: str = ""


@dataclass
class PreprocessConfig:
    """Configuration for standardized Fundus preprocessing."""
    target_width: int = 512
    target_height: int = 512
    crop_fov: bool = True
    fov_padding: int = 8
    fov_threshold: int = 15
    enhance_contrast: bool = True
    contrast_clip_percent: float = 1.0
    unsharp_radius: float = 2.0
    unsharp_percent: int = 140
    min_luminance: float = 15.0
    max_luminance: float = 240.0
    min_contrast: float = 8.0
    min_sharpness: float = 3.5
    min_fov_ratio: float = 0.15

    def __post_init__(self) -> None:
        self.validate()

    def validate(self) -> None:
        """Reject invalid configuration, including NaN, before image work starts."""
        for name in ("target_width", "target_height"):
            value = getattr(self, name)
            if type(value) is not int or value <= 0:
                raise ValueError(f"{name} must be a positive integer")
        for name in ("fov_padding", "unsharp_percent"):
            value = getattr(self, name)
            if type(value) is not int or value < 0:
                raise ValueError(f"{name} must be a non-negative integer")
        if type(self.fov_threshold) is not int or not 0 <= self.fov_threshold <= 255:
            raise ValueError("fov_threshold must be an integer in [0, 255]")
        for name in ("crop_fov", "enhance_contrast"):
            if type(getattr(self, name)) is not bool:
                raise ValueError(f"{name} must be a boolean")
        for name in ("contrast_clip_percent", "unsharp_radius", "min_luminance",
                     "max_luminance", "min_contrast", "min_sharpness", "min_fov_ratio"):
            value = getattr(self, name)
            if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
                raise ValueError(f"{name} must be a finite number")
        if not 0 <= self.contrast_clip_percent < 50:
            raise ValueError("contrast_clip_percent must be in [0, 50)")
        if self.unsharp_radius < 0:
            raise ValueError("unsharp_radius must be non-negative")
        if not 0 <= self.min_luminance <= self.max_luminance <= 255:
            raise ValueError("luminance thresholds must be ordered within [0, 255]")
        if not 0 <= self.min_contrast <= 255 or not 0 <= self.min_sharpness <= 255:
            raise ValueError("contrast and sharpness thresholds must be within [0, 255]")
        if not 0 <= self.min_fov_ratio <= 1:
            raise ValueError("min_fov_ratio must be in [0, 1]")


@dataclass
class PreprocessResult:
    """Output artifact of the Fundus preprocessing pipeline."""
    original_size: Tuple[int, int]
    processed_size: Tuple[int, int]
    enhanced_image: Image.Image
    green_channel: Image.Image
    fov_mask: Image.Image
    quality: QualityMetrics
    is_acceptable: bool
    rejection_reason: Optional[str] = None

    def to_base64_png(self, target: str = "enhanced") -> str:
        """Helper to serialize preprocessed image or mask to base64 PNG."""
        img = {
            "enhanced": self.enhanced_image,
            "green": self.green_channel,
            "mask": self.fov_mask,
        }.get(target, self.enhanced_image)

        buffer = io.BytesIO()
        img.save(buffer, format="PNG", optimize=True)
        return base64.b64encode(buffer.getvalue()).decode("ascii")


def load_image(input_data: Union[Image.Image, bytes, str]) -> Image.Image:
    """Load image from PIL Image, raw bytes, or base64 string safely."""
    if isinstance(input_data, Image.Image):
        return input_data.convert("RGB")

    if isinstance(input_data, str):
        clean_str = input_data.strip()
        if "," in clean_str:
            clean_str = clean_str.split(",", 1)[1].strip()
        raw_bytes = base64.b64decode(clean_str, validate=True)
    elif isinstance(input_data, (bytes, bytearray)):
        raw_bytes = bytes(input_data)
    else:
        raise TypeError(f"Unsupported image input type: {type(input_data)}")

    with Image.open(io.BytesIO(raw_bytes)) as img:
        img.verify()
    with Image.open(io.BytesIO(raw_bytes)) as img:
        img.load()
        return img.convert("RGB")


def detect_fov_mask(
    image: Image.Image,
    threshold: int = 15,
    morph_iterations: int = 3,
) -> Image.Image:
    """
    Detect the circular Field of View (FOV) mask for fundus photography.
    The red channel typically provides the highest intensity against dark background.
    """
    r_channel, g_channel, _ = image.split()
    # Combine Red and Green to capture full illuminated retina tissue
    combined = ImageChops.add(r_channel, g_channel, scale=1.0, offset=0)

    # Thresholding: 255 if above threshold, 0 if background
    mask = combined.point(lambda p: 255 if p > threshold else 0, mode="L")

    # Morphological closing (MaxFilter then MinFilter) to bridge holes (vessels, dark lesions)
    for _ in range(morph_iterations):
        mask = mask.filter(ImageFilter.MaxFilter(size=5))
    for _ in range(morph_iterations):
        mask = mask.filter(ImageFilter.MinFilter(size=5))

    return mask


def crop_to_fov(
    image: Image.Image,
    mask: Image.Image,
    padding: int = 8,
) -> Tuple[Image.Image, Image.Image]:
    """
    Crop the image and mask tightly to the detected FOV bounding box,
    maintaining a square 1:1 aspect ratio.
    """
    bbox = mask.getbbox()
    if not bbox:
        # Fallback if no FOV detected
        w, h = image.size
        side = min(w, h)
        left = (w - side) // 2
        top = (h - side) // 2
        crop_box = (left, top, left + side, top + side)
        return image.crop(crop_box), mask.crop(crop_box)

    min_x, min_y, max_x, max_y = bbox
    min_x = max(0, min_x - padding)
    min_y = max(0, min_y - padding)
    max_x = min(image.width, max_x + padding)
    max_y = min(image.height, max_y + padding)

    width = max_x - min_x
    height = max_y - min_y
    side = max(width, height)

    center_x = (min_x + max_x) // 2
    center_y = (min_y + max_y) // 2

    square_min_x = center_x - side // 2
    square_min_y = center_y - side // 2
    square_max_x = square_min_x + side
    square_max_y = square_min_y + side

    # Pad with black border if square bounding box exceeds image boundary
    delta_left = max(0, -square_min_x)
    delta_top = max(0, -square_min_y)
    delta_right = max(0, square_max_x - image.width)
    delta_bottom = max(0, square_max_y - image.height)

    if delta_left > 0 or delta_top > 0 or delta_right > 0 or delta_bottom > 0:
        new_w = image.width + delta_left + delta_right
        new_h = image.height + delta_top + delta_bottom
        padded_img = Image.new("RGB", (new_w, new_h), color=(0, 0, 0))
        padded_img.paste(image, (delta_left, delta_top))

        padded_mask = Image.new("L", (new_w, new_h), color=0)
        padded_mask.paste(mask, (delta_left, delta_top))

        crop_box = (
            square_min_x + delta_left,
            square_min_y + delta_top,
            square_max_x + delta_left,
            square_max_y + delta_top,
        )
        return padded_img.crop(crop_box), padded_mask.crop(crop_box)

    crop_box = (square_min_x, square_min_y, square_max_x, square_max_y)
    return image.crop(crop_box), mask.crop(crop_box)


def enhance_fundus_contrast(
    green_channel: Image.Image,
    mask: Image.Image,
    clip_percent: float = 1.0,
    unsharp_radius: float = 2.0,
    unsharp_percent: int = 140,
) -> Image.Image:
    """
    Enhance vessel contrast on the green channel using adaptive histogram stretching
    and high-pass unsharp masking within the illuminated FOV region.
    """
    # 1. Contrast stretch with clipping
    stretched = ImageOps.autocontrast(green_channel, cutoff=clip_percent, mask=mask)

    # 2. High-pass unsharp mask to accentuate thin vessel edges
    sharpened = stretched.filter(
        ImageFilter.UnsharpMask(
            radius=unsharp_radius,
            percent=unsharp_percent,
            threshold=3,
        )
    )

    # 3. Mask out non-retina background to keep it pure black
    result = Image.composite(sharpened, Image.new("L", sharpened.size, color=0), mask)
    return result


def compute_sharpness_score(gray_image: Image.Image, mask: Image.Image) -> float:
    """
    Compute edge sharpness score using high-pass Laplacian kernel approximation.
    Higher values indicate sharp vessel boundaries; low values indicate motion blur or haze.
    """
    edges = gray_image.filter(ImageFilter.FIND_EDGES)
    masked_edges = Image.composite(edges, Image.new("L", edges.size, color=0), mask)
    stat = ImageStat.Stat(masked_edges, mask=mask)
    # Mean edge intensity within FOV
    return float(stat.mean[0]) if stat.mean else 0.0


def evaluate_quality(
    image: Image.Image,
    mask: Image.Image,
    config: PreprocessConfig,
) -> Tuple[QualityMetrics, bool, Optional[str]]:
    """
    Assess image quality with configurable computer vision heuristics:
    - FOV area ratio (checks if camera was properly aligned)
    - Mean luminance within FOV (detects underexposed/overexposed flash)
    - Contrast score (standard deviation of pixel intensity inside retina)
    - Sharpness score (edge frequency strength)
    """
    config.validate()
    total_pixels = image.width * image.height

    # Calculate precise non-zero mask pixels
    stat_mask = ImageStat.Stat(mask)
    fov_pixels = int(stat_mask.sum[0] / 255.0) if stat_mask.sum else 0
    fov_ratio = fov_pixels / max(1, total_pixels)

    gray = image.convert("L")
    stat = ImageStat.Stat(gray, mask=mask if fov_pixels > 0 else None)

    mean_lum = float(stat.mean[0]) if stat.mean else 0.0
    contrast = float(stat.stddev[0]) if stat.stddev else 0.0
    sharpness = compute_sharpness_score(gray, mask)

    is_valid_fundus = True
    rejection_reason = None

    if fov_pixels == 0 or fov_ratio < config.min_fov_ratio:
        is_valid_fundus = False
        rejection_reason = (
            f"Vùng quan sát võng mạc (FOV) quá nhỏ ({fov_ratio*100:.1f}% < {config.min_fov_ratio*100:.1f}%)."
        )
    elif mean_lum < config.min_luminance:
        is_valid_fundus = False
        rejection_reason = (
            f"Ảnh quá tối hoặc thiếu sáng nghiêm trọng (Độ sáng {mean_lum:.1f} < {config.min_luminance:.1f})."
        )
    elif mean_lum > config.max_luminance:
        is_valid_fundus = False
        rejection_reason = (
            f"Ảnh bị lóa hoặc thừa sáng/cháy sáng (Độ sáng {mean_lum:.1f} > {config.max_luminance:.1f})."
        )
    elif contrast < config.min_contrast:
        is_valid_fundus = False
        rejection_reason = (
            f"Độ tương phản ảnh quá thấp, không phân biệt được cấu trúc đáy mắt (Contrast {contrast:.1f} < {config.min_contrast:.1f})."
        )
    elif sharpness < config.min_sharpness:
        is_valid_fundus = False
        rejection_reason = (
            f"Ảnh bị mờ nhòe hoặc mất nét nghiêm trọng (Độ sắc nét {sharpness:.1f} < {config.min_sharpness:.1f})."
        )

    # Assign rating
    if not is_valid_fundus:
        quality_rating = QualityRating.INVALID
    elif mean_lum >= 35.0 and mean_lum <= 200.0 and contrast >= 25.0 and sharpness >= 8.0:
        quality_rating = QualityRating.GOOD
    elif contrast >= 15.0 and sharpness >= 5.0:
        quality_rating = QualityRating.ACCEPTABLE
    else:
        quality_rating = QualityRating.POOR

    metrics = QualityMetrics(
        mean_luminance=round(mean_lum, 2),
        contrast_score=round(contrast, 2),
        sharpness_score=round(sharpness, 2),
        fov_ratio=round(fov_ratio, 4),
        is_valid_fundus=is_valid_fundus,
        quality_rating=quality_rating,
        details=rejection_reason or f"Chất lượng ảnh đạt mức {quality_rating.value}.",
    )

    return metrics, is_valid_fundus, rejection_reason


def preprocess_fundus_image(
    image_input: Union[Image.Image, bytes, str],
    config: Optional[PreprocessConfig] = None,
) -> PreprocessResult:
    """
    Main entry point for standardized Fundus Preprocessing Pipeline.

    Pipeline Steps:
    1. Load, verify, and normalize input to RGB PIL Image.
    2. Segment and extract binary FOV mask.
    3. Evaluate initial quality metrics (reject early if invalid).
    4. Auto-crop & center image on FOV bounding box with square padding.
    5. Resize image and mask to target dimensions using high-quality Lanczos filter.
    6. Extract Green channel (optimal vascular absorption).
    7. Enhance contrast and sharpen vascular structures.
    8. Package output artifact with comprehensive metadata.
    """
    if config is None:
        config = PreprocessConfig()
    config.validate()  # Also catches mutation of this intentionally mutable dataclass.

    image = load_image(image_input)
    original_size = image.size

    # Step 1: Detect FOV Mask on original resolution
    fov_mask = detect_fov_mask(image, threshold=config.fov_threshold)

    # Step 2: Quality assessment
    quality, is_acceptable, rejection_reason = evaluate_quality(image, fov_mask, config)

    # Step 3: Crop to FOV if enabled
    if config.crop_fov and fov_mask.getbbox() is not None:
        cropped_image, cropped_mask = crop_to_fov(image, fov_mask, padding=config.fov_padding)
    else:
        cropped_image, cropped_mask = image, fov_mask

    # Step 4: Resolution standardization (Resize to target_width x target_height)
    target_dim = (config.target_width, config.target_height)
    resized_image = cropped_image.resize(target_dim, resample=Image.Resampling.LANCZOS)
    resized_mask = cropped_mask.resize(target_dim, resample=Image.Resampling.NEAREST)

    # Step 5: Extract Green channel
    r, g, b = resized_image.split()

    # Step 6: Contrast enhancement
    if config.enhance_contrast:
        enhanced = enhance_fundus_contrast(
            g,
            resized_mask,
            clip_percent=config.contrast_clip_percent,
            unsharp_radius=config.unsharp_radius,
            unsharp_percent=config.unsharp_percent,
        )
    else:
        enhanced = g

    return PreprocessResult(
        original_size=original_size,
        processed_size=target_dim,
        enhanced_image=enhanced,
        green_channel=g,
        fov_mask=resized_mask,
        quality=quality,
        is_acceptable=is_acceptable,
        rejection_reason=rejection_reason,
    )
