"""
Unit tests for standardized Fundus Preprocessing Module.
Covers:
- Loading diverse input formats (PIL, bytes, base64)
- FOV detection & binary mask generation
- Centering, square cropping & padding
- Green channel extraction & contrast enhancement
- Multi-dimensional image quality assessment (underexposure, overexposure, blur, small FOV)
- End-to-end pipeline execution and output artifact serialization
"""

import base64
import io
import pytest
from PIL import Image, ImageDraw

from app.services.preprocessing import (
    PreprocessConfig,
    PreprocessResult,
    QualityRating,
    crop_to_fov,
    detect_fov_mask,
    enhance_fundus_contrast,
    evaluate_quality,
    load_image,
    preprocess_fundus_image,
)


def create_synthetic_fundus(
    width: int = 600,
    height: int = 400,
    circle_radius: int = 160,
    color: tuple = (200, 100, 50),
    add_vessels: bool = True,
    bg_color: tuple = (0, 0, 0),
) -> Image.Image:
    """Create a synthetic fundus-like image with a colored retina disc and vessel lines."""
    img = Image.new("RGB", (width, height), color=bg_color)
    draw = ImageDraw.Draw(img)
    cx, cy = width // 2, height // 2

    # Draw circular retina FOV
    draw.ellipse(
        [cx - circle_radius, cy - circle_radius, cx + circle_radius, cy + circle_radius],
        fill=color,
    )

    if add_vessels:
        # Draw high-contrast vessel-like structures in darker tones
        vessel_color = (max(0, color[0] - 120), max(0, color[1] - 80), max(0, color[2] - 40))
        for offset in range(-80, 90, 30):
            draw.line([cx + offset, cy - 120, cx - offset, cy + 120], fill=vessel_color, width=4)
            draw.line([cx - 120, cy + offset, cx + 120, cy - offset], fill=vessel_color, width=3)
        # Optic disc (bright oval)
        draw.ellipse([cx + 60, cy - 30, cx + 110, cy + 20], fill=(255, 240, 180))
        # Macula (darker spot)
        draw.ellipse([cx - 50, cy - 20, cx - 10, cy + 20], fill=(120, 50, 20))

    return img


class TestImageLoading:
    def test_load_from_pil(self):
        img = Image.new("RGB", (100, 100), color=(100, 50, 20))
        loaded = load_image(img)
        assert loaded.size == (100, 100)
        assert loaded.mode == "RGB"

    def test_load_from_bytes(self):
        img = Image.new("RGB", (50, 50), color=(50, 120, 200))
        buffer = io.BytesIO()
        img.save(buffer, format="PNG")
        loaded = load_image(buffer.getvalue())
        assert loaded.size == (50, 50)

    def test_load_from_base64_plain(self):
        img = Image.new("RGB", (64, 64), color=(200, 150, 80))
        buffer = io.BytesIO()
        img.save(buffer, format="JPEG")
        b64 = base64.b64encode(buffer.getvalue()).decode("ascii")
        loaded = load_image(b64)
        assert loaded.size == (64, 64)

    def test_load_from_base64_data_url(self):
        img = Image.new("RGB", (32, 32), color=(10, 20, 30))
        buffer = io.BytesIO()
        img.save(buffer, format="PNG")
        b64 = "data:image/png;base64," + base64.b64encode(buffer.getvalue()).decode("ascii")
        loaded = load_image(b64)
        assert loaded.size == (32, 32)

    def test_invalid_type_raises_error(self):
        with pytest.raises(TypeError):
            load_image(12345)  # type: ignore


class TestFovDetectionAndCropping:
    def test_detect_fov_mask(self):
        fundus = create_synthetic_fundus(width=400, height=400, circle_radius=150)
        mask = detect_fov_mask(fundus, threshold=15)

        assert mask.size == fundus.size
        assert mask.mode == "L"

        bbox = mask.getbbox()
        assert bbox is not None
        # Center of mask should be inside the detected FOV
        assert mask.getpixel((200, 200)) == 255
        # Corner should be black (background)
        assert mask.getpixel((10, 10)) == 0

    def test_crop_to_fov_square_aspect(self):
        fundus = create_synthetic_fundus(width=600, height=400, circle_radius=140)
        mask = detect_fov_mask(fundus)
        cropped_img, cropped_mask = crop_to_fov(fundus, mask, padding=5)

        assert cropped_img.width == cropped_img.height
        assert cropped_mask.width == cropped_mask.height
        assert cropped_img.size == cropped_mask.size
        # Cropped dimension should be smaller than original width (600)
        assert cropped_img.width <= 600


class TestContrastEnhancement:
    def test_green_channel_contrast_enhancement(self):
        fundus = create_synthetic_fundus(width=300, height=300, circle_radius=120)
        mask = detect_fov_mask(fundus)
        _, g, _ = fundus.split()

        enhanced = enhance_fundus_contrast(g, mask)
        assert enhanced.size == g.size
        assert enhanced.mode == "L"

        # Background area outside FOV should remain 0 (black)
        assert enhanced.getpixel((5, 5)) == 0


class TestQualityAssessment:
    def test_valid_fundus_quality(self):
        fundus = create_synthetic_fundus(width=500, height=500, circle_radius=200, add_vessels=True)
        mask = detect_fov_mask(fundus)
        config = PreprocessConfig()
        metrics, is_acceptable, reason = evaluate_quality(fundus, mask, config)

        assert is_acceptable is True
        assert reason is None
        assert metrics.is_valid_fundus is True
        assert metrics.quality_rating in (QualityRating.GOOD, QualityRating.ACCEPTABLE)
        assert metrics.mean_luminance > config.min_luminance
        assert metrics.fov_ratio >= config.min_fov_ratio

    def test_reject_underexposed_image(self):
        # Very dark image
        dark_fundus = create_synthetic_fundus(
            width=400, height=400, circle_radius=150, color=(10, 5, 2), add_vessels=False
        )
        mask = detect_fov_mask(dark_fundus, threshold=5)
        config = PreprocessConfig(min_luminance=20.0)
        metrics, is_acceptable, reason = evaluate_quality(dark_fundus, mask, config)

        assert is_acceptable is False
        assert metrics.quality_rating == QualityRating.INVALID
        assert "quá tối" in (reason or "")

    def test_reject_overexposed_image(self):
        # Blown out flash
        bright_fundus = Image.new("RGB", (400, 400), color=(250, 250, 250))
        mask = Image.new("L", (400, 400), color=255)
        config = PreprocessConfig(max_luminance=230.0)
        metrics, is_acceptable, reason = evaluate_quality(bright_fundus, mask, config)

        assert is_acceptable is False
        assert "thừa sáng" in (reason or "") or "cháy sáng" in (reason or "")

    def test_reject_tiny_fov(self):
        # FOV circle radius 15px on a 500x500 image -> ratio ~0.003
        tiny_fundus = create_synthetic_fundus(width=500, height=500, circle_radius=15)
        mask = detect_fov_mask(tiny_fundus)
        config = PreprocessConfig(min_fov_ratio=0.15)
        metrics, is_acceptable, reason = evaluate_quality(tiny_fundus, mask, config)

        assert is_acceptable is False
        assert "quá nhỏ" in (reason or "")


class TestEndToEndPipeline:
    def test_full_preprocessing_pipeline(self):
        fundus = create_synthetic_fundus(width=700, height=500, circle_radius=180, add_vessels=True)
        config = PreprocessConfig(target_width=512, target_height=512)

        result: PreprocessResult = preprocess_fundus_image(fundus, config)

        assert result.original_size == (700, 500)
        assert result.processed_size == (512, 512)
        assert result.enhanced_image.size == (512, 512)
        assert result.green_channel.size == (512, 512)
        assert result.fov_mask.size == (512, 512)
        assert result.is_acceptable is True

        # Test Base64 serialization
        b64_enhanced = result.to_base64_png("enhanced")
        b64_mask = result.to_base64_png("mask")

        assert isinstance(b64_enhanced, str) and len(b64_enhanced) > 50
        assert isinstance(b64_mask, str) and len(b64_mask) > 50

    def test_pipeline_with_base64_input(self):
        fundus = create_synthetic_fundus(width=400, height=400, circle_radius=140)
        buf = io.BytesIO()
        fundus.save(buf, format="JPEG")
        b64_str = base64.b64encode(buf.getvalue()).decode("ascii")

        result = preprocess_fundus_image(b64_str)
        assert result.processed_size == (512, 512)
        assert result.quality.is_valid_fundus is True
