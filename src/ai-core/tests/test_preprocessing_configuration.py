"""Regression tests for invalid configuration and empty FOV, using synthetic images."""
import math
import pytest
from PIL import Image
from app.services.preprocessing import PreprocessConfig, QualityRating, evaluate_quality, preprocess_fundus_image


@pytest.mark.parametrize("values", [
    {"target_width": 0}, {"target_height": -1}, {"target_width": True},
    {"target_height": 32.5}, {"fov_padding": -1}, {"fov_threshold": 256},
    {"fov_threshold": -1}, {"crop_fov": "false"}, {"enhance_contrast": 1},
    {"contrast_clip_percent": 50}, {"contrast_clip_percent": -1},
    {"unsharp_radius": -1}, {"unsharp_percent": -1},
    {"min_luminance": 200, "max_luminance": 100}, {"max_luminance": 256},
    {"min_contrast": -1}, {"min_sharpness": 256}, {"min_fov_ratio": 1.1},
    {"min_fov_ratio": -0.1}, {"min_luminance": "15"},
])
def test_invalid_configuration_rejected(values):
    with pytest.raises(ValueError):
        PreprocessConfig(**values)


@pytest.mark.parametrize("name", ["contrast_clip_percent", "unsharp_radius", "min_luminance",
    "max_luminance", "min_contrast", "min_sharpness", "min_fov_ratio"])
@pytest.mark.parametrize("value", [float("nan"), float("inf"), float("-inf")])
def test_nonfinite_thresholds_cannot_disable_checks(name, value):
    with pytest.raises(ValueError):
        PreprocessConfig(**{name: value})


def test_mutated_config_is_rejected_before_image_loading():
    config = PreprocessConfig()
    config.min_sharpness = float("nan")
    with pytest.raises(ValueError, match="min_sharpness"):
        preprocess_fundus_image(b"not-an-image", config)


def test_direct_quality_call_revalidates_mutated_config():
    config = PreprocessConfig()
    config.min_fov_ratio = float("nan")
    with pytest.raises(ValueError, match="min_fov_ratio"):
        evaluate_quality(Image.new("RGB", (32, 32)), Image.new("L", (32, 32)), config)


def test_empty_fov_is_invalid_even_when_quality_thresholds_are_zero():
    config = PreprocessConfig(min_luminance=0, max_luminance=255,
        min_contrast=0, min_sharpness=0, min_fov_ratio=0)
    result = preprocess_fundus_image(Image.new("RGB", (32, 32)), config)
    assert result.is_acceptable is False
    assert result.quality.is_valid_fundus is False
    assert result.quality.quality_rating is QualityRating.INVALID
    assert result.rejection_reason is not None
    assert result.quality.fov_ratio == 0
    assert all(math.isfinite(v) for v in [result.quality.mean_luminance,
        result.quality.contrast_score, result.quality.sharpness_score])
