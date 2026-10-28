"""
Supervised Retinal Vessel Segmentation Smoke-Training Pipeline (Module M4 - SCRUM-330 / W3-M4-02).

Purpose:
- Verify the end-to-end data loading, subject-level split isolation (seed=42),
  feature/patch batching, supervised binary cross-entropy optimization, and
  validation metric tracking on CHASE_DB1 `train` and `validation` splits ONLY.
- Strictly forbids touching the `test` split (`test_split_touched = False`).
- Strictly forbids synthesizing or inferring clinical risk labels (`risk_labels_used = False`).
- Does NOT replace the frozen Sprint 3 runtime baseline (`frangi-v0.2`).
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import platform
import time
import zipfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Tuple

import numpy as np
import PIL
import scipy
import skimage
import yaml
from PIL import Image
from skimage.filters import gaussian

ROOT = Path(__file__).resolve().parents[3]
DEFAULT_CONFIG_PATH = Path(__file__).resolve().parents[1] / "config" / "segmentation-training-smoke.yaml"


def ensure_chase_db1_extracted() -> Path:
    raw_dir = ROOT / "datasets" / "CHASE_DB1" / "raw"
    archive_path = ROOT / "data-assets" / "CHASE_DB1" / "CHASEDB1.zip"
    if not (raw_dir / "Image_01L.jpg").is_file() and archive_path.is_file():
        raw_dir.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(archive_path, "r") as zf:
            zf.extractall(raw_dir)
    return raw_dir


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify_and_load_manifest(
    manifest_path: Path,
    allowed_splits: List[str],
    forbidden_splits: List[str],
) -> Tuple[List[Dict[str, str]], List[Dict[str, str]], Dict[str, List[str]]]:
    if not manifest_path.is_file():
        raise FileNotFoundError(f"Dataset manifest not found: {manifest_path}")

    with manifest_path.open("r", encoding="utf-8-sig", newline="") as f:
        rows = list(csv.DictReader(f))

    if len(rows) != 28:
        raise ValueError(f"Expected 28 rows in CHASE_DB1 manifest, got {len(rows)}")

    # Verify zero subject leakage across all splits
    subject_to_splits: Dict[str, set] = {}
    split_subjects: Dict[str, set] = {"train": set(), "validation": set(), "test": set()}
    for row in rows:
        subj = row["subject_id"]
        sp = row["split"]
        subject_to_splits.setdefault(subj, set()).add(sp)
        split_subjects.setdefault(sp, set()).add(subj)

    for subj, sp_set in subject_to_splits.items():
        if len(sp_set) != 1:
            raise ValueError(f"Subject leakage detected for subject {subj}: {sp_set}")

    if split_subjects["train"] & split_subjects["validation"]:
        raise ValueError("Train/validation subject overlap detected")
    if split_subjects["train"] & split_subjects["test"]:
        raise ValueError("Train/test subject overlap detected")
    if split_subjects["validation"] & split_subjects["test"]:
        raise ValueError("Validation/test subject overlap detected")

    # Filter strictly to allowed splits (train and validation), never touching test files
    train_rows = [r for r in rows if r["split"] == "train" and r["split"] in allowed_splits]
    val_rows = [r for r in rows if r["split"] == "validation" and r["split"] in allowed_splits]

    for r in train_rows + val_rows:
        if r["split"] in forbidden_splits:
            raise RuntimeError(f"Forbidden split '{r['split']}' was selected!")

    return (
        train_rows,
        val_rows,
        {k: sorted(v) for k, v in split_subjects.items()},
    )


def extract_multiscale_features(
    rgb: np.ndarray,
    sigmas: List[float],
    stride: int = 1,
) -> np.ndarray:
    """
    Extract deterministic multiscale vessel features from the green channel (normalized to [0, 1]).
    For each sigma, computes local background subtraction (local_mean - green_smoothed)
    capturing dark tubular ridges on bright retinal background.
    """
    green = rgb[::stride, ::stride, 1].astype(np.float64) / 255.0
    features = [1.0 - green]
    for sigma in sigmas:
        smoothed = gaussian(green, sigma=sigma, preserve_range=True, mode="nearest")
        background = gaussian(green, sigma=sigma * 3.0, preserve_range=True, mode="nearest")
        ridge_contrast = np.clip(background - smoothed, 0.0, 1.0)
        features.append(ridge_contrast)
    stacked = np.stack(features, axis=-1)
    # Standardize features per-image for stable gradient descent
    mean = np.mean(stacked, axis=(0, 1), keepdims=True)
    std = np.std(stacked, axis=(0, 1), keepdims=True) + 1e-6
    return (stacked - mean) / std


def load_split_dataset(
    rows: List[Dict[str, str]],
    reference_mask_key: str,
    sigmas: List[float],
    stride: int,
) -> Tuple[np.ndarray, np.ndarray, List[str]]:
    x_list = []
    y_list = []
    loaded_images: List[str] = []

    for row in rows:
        if row["split"] == "test":
            raise RuntimeError("Attempted to load a test split image during training smoke run!")

        img_path = ROOT / row["image"]
        mask_path = ROOT / row[reference_mask_key]

        if sha256_file(img_path) != row["image_sha256"]:
            raise ValueError(f"SHA256 mismatch for {img_path}")
        if sha256_file(mask_path) != row[f"{reference_mask_key}_sha256"]:
            raise ValueError(f"SHA256 mismatch for {mask_path}")

        with Image.open(img_path) as img:
            rgb = np.asarray(img.convert("RGB"), dtype=np.uint8)
        with Image.open(mask_path) as m_img:
            mask = np.asarray(m_img.convert("L"), dtype=np.uint8)

        feats = extract_multiscale_features(rgb, sigmas=sigmas, stride=stride)
        labels = (mask[::stride, ::stride] > 0).astype(np.float64)

        x_list.append(feats.reshape(-1, feats.shape[-1]))
        y_list.append(labels.reshape(-1))
        loaded_images.append(img_path.name)

    return np.concatenate(x_list, axis=0), np.concatenate(y_list, axis=0), loaded_images


def sigmoid(z: np.ndarray) -> np.ndarray:
    return 1.0 / (1.0 + np.exp(-np.clip(z, -30.0, 30.0)))


def weighted_bce_loss(
    probs: np.ndarray,
    targets: np.ndarray,
    pos_weight: float,
    weights: np.ndarray,
    l2_reg: float,
) -> float:
    eps = 1e-7
    p = np.clip(probs, eps, 1.0 - eps)
    bce = -(pos_weight * targets * np.log(p) + (1.0 - targets) * np.log(1.0 - p))
    reg_term = 0.5 * l2_reg * float(np.sum(weights ** 2))
    return float(np.mean(bce)) + reg_term


def compute_binary_segmentation_metrics(probs: np.ndarray, targets: np.ndarray, threshold: float) -> Dict[str, float]:
    preds = probs >= threshold
    truth = targets >= 0.5
    tp = int(np.sum(preds & truth))
    fp = int(np.sum(preds & ~truth))
    fn = int(np.sum(~preds & truth))
    tn = int(np.sum(~preds & ~truth))
    dice = (2.0 * tp) / max(1, 2 * tp + fp + fn)
    iou = float(tp) / max(1, tp + fp + fn)
    sensitivity = float(tp) / max(1, tp + fn)
    specificity = float(tn) / max(1, tn + fp)
    return {
        "dice": round(float(dice), 6),
        "iou": round(float(iou), 6),
        "sensitivity": round(float(sensitivity), 6),
        "specificity": round(float(specificity), 6),
    }


def run_smoke_training(config_path: Path = DEFAULT_CONFIG_PATH) -> Dict[str, Any]:
    start_time = time.perf_counter()
    ensure_chase_db1_extracted()
    cfg = yaml.safe_load(config_path.read_text(encoding="utf-8"))

    if cfg.get("replacesRuntimeFrangi") is not False:
        raise ValueError("Smoke training scaffold must not replace frozen Frangi runtime.")
    if cfg.get("riskLabelsAllowed") is not False or cfg.get("clinicalRiskInference") is not False:
        raise ValueError("Smoke training scaffold must not synthesize or infer clinical risk labels.")

    ds_cfg = cfg["dataset"]
    tr_cfg = cfg["training"]

    manifest_path = ROOT / ds_cfg["manifestPath"]
    train_rows, val_rows, split_subjects = verify_and_load_manifest(
        manifest_path=manifest_path,
        allowed_splits=list(ds_cfg["allowedSplits"]),
        forbidden_splits=list(ds_cfg["forbiddenSplits"]),
    )

    sigmas = [float(s) for s in tr_cfg["featureScalesSigma"]]
    stride = int(tr_cfg["sampleStride"])
    epochs = int(tr_cfg["epochs"])
    lr = float(tr_cfg["learningRate"])
    l2_reg = float(tr_cfg["l2Regularization"])
    pos_weight = float(tr_cfg["positiveClassWeight"])
    threshold = float(tr_cfg["decisionThreshold"])

    x_train, y_train, train_images = load_split_dataset(
        train_rows,
        reference_mask_key=ds_cfg["referenceMask"],
        sigmas=sigmas,
        stride=stride,
    )
    x_val, y_val, val_images = load_split_dataset(
        val_rows,
        reference_mask_key=ds_cfg["referenceMask"],
        sigmas=sigmas,
        stride=stride,
    )

    rng = np.random.default_rng(int(ds_cfg["seed"]))
    num_features = x_train.shape[1]
    weights = rng.normal(loc=0.0, scale=0.01, size=(num_features,))
    bias = -1.0

    epoch_history: List[Dict[str, Any]] = []
    n_samples = float(x_train.shape[0])

    for epoch in range(1, epochs + 1):
        logits_train = x_train @ weights + bias
        probs_train = sigmoid(logits_train)
        train_loss = weighted_bce_loss(probs_train, y_train, pos_weight, weights, l2_reg)

        sample_weights = np.where(y_train >= 0.5, pos_weight, 1.0)
        error = (probs_train - y_train) * sample_weights
        grad_w = (x_train.T @ error) / n_samples + l2_reg * weights
        grad_b = float(np.mean(error))

        weights = weights - lr * grad_w
        bias = bias - lr * grad_b

        logits_val = x_val @ weights + bias
        probs_val = sigmoid(logits_val)
        val_loss = weighted_bce_loss(probs_val, y_val, pos_weight, weights, l2_reg)
        val_metrics = compute_binary_segmentation_metrics(probs_val, y_val, threshold)

        epoch_history.append(
            {
                "epoch": epoch,
                "train_weighted_bce_loss": round(train_loss, 6),
                "val_weighted_bce_loss": round(val_loss, 6),
                "val_dice": val_metrics["dice"],
                "val_iou": val_metrics["iou"],
                "val_sensitivity": val_metrics["sensitivity"],
                "val_specificity": val_metrics["specificity"],
            }
        )

    elapsed_ms = round((time.perf_counter() - start_time) * 1000.0, 2)
    loss_decreased = epoch_history[-1]["train_weighted_bce_loss"] < epoch_history[0]["train_weighted_bce_loss"]

    report: Dict[str, Any] = {
        "status": "SEGMENTATION_TRAINING_SMOKE_PASSED" if loss_decreased else "SEGMENTATION_TRAINING_SMOKE_FAILED",
        "pipeline_version": cfg["pipelineVersion"],
        "task": cfg["task"],
        "timestamp_utc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "replaces_runtime_frangi": False,
        "runtime_model_version_preserved": "frangi-v0.2",
        "risk_available": False,
        "risk_labels_used": False,
        "clinical_risk_inference": False,
        "dataset": {
            "name": ds_cfg["name"],
            "seed": int(ds_cfg["seed"]),
            "split_type": "project_defined_subject_split",
            "reference_mask": ds_cfg["referenceMask"],
            "manifest_sha256": sha256_file(manifest_path),
            "split_subjects": split_subjects,
            "train_image_count": len(train_images),
            "validation_image_count": len(val_images),
            "test_images_loaded": 0,
            "test_split_touched": False,
            "subject_leakage_detected": False,
            "train_images": train_images,
            "validation_images": val_images,
        },
        "environment": {
            "os": platform.platform(),
            "python": platform.python_version(),
            "Pillow": PIL.__version__,
            "numpy": np.__version__,
            "scipy": scipy.__version__,
            "scikit_image": skimage.__version__,
        },
        "training_config": tr_cfg,
        "epoch_history": epoch_history,
        "loss_decreased": bool(loss_decreased),
        "final_validation_metrics": epoch_history[-1],
        "execution_time_ms": elapsed_ms,
        "notes": (
            "Smoke-run scaffold for supervised vessel segmentation on CHASE_DB1 train/validation splits only. "
            "Test split was never loaded or touched. No clinical risk labels exist or were inferred. "
            "Sprint 3 runtime inference continues to use frozen Frangi baseline (frangi-v0.2)."
        ),
    }

    output_path = ROOT / cfg["output"]["reportPath"]
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(
        json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False) + "\n",
        encoding="utf-8",
    )
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description="Run CHASE_DB1 segmentation training smoke pipeline")
    parser.add_argument(
        "--config",
        default=str(DEFAULT_CONFIG_PATH),
        help="Path to segmentation-training-smoke.yaml",
    )
    args = parser.parse_args()
    report = run_smoke_training(Path(args.config))
    print(json.dumps(report, ensure_ascii=False, indent=2))
    if not report["loss_decreased"] or report["dataset"]["test_split_touched"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()

