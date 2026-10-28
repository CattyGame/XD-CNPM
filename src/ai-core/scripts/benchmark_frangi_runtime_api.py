"""
Sprint 3 Runtime API Benchmark for Real Frangi v0.2 Endpoint (Module M4 - SCRUM-331 / W3-M4-03).

Measures end-to-end AI service HTTP processing (`POST /api/v1/analyze`) on the 6 frozen CHASE_DB1
test images (3 subjects: 01, 02, 11) with full RS256 Service JWT verification, Base64 decoding,
pre-Frangi quality assessment, Frangi v0.2 segmentation, PNG mask & overlay encoding, and
JSON Schema 2020-12 contract validation (`ai-request-1.2.schema.json` & `ai-response-1.2.schema.json`).
"""

from __future__ import annotations

import base64
import csv
import hashlib
import io
import json
import os
import platform
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List

import jsonschema
import numpy as np
import PIL
import scipy
import skimage
from fastapi.testclient import TestClient
from PIL import Image

SCRIPT_DIR = Path(__file__).resolve().parent
AI_CORE_DIR = SCRIPT_DIR.parent
REPO_ROOT = AI_CORE_DIR.parent.parent
if str(AI_CORE_DIR) not in sys.path:
    sys.path.insert(0, str(AI_CORE_DIR))
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import baseline_benchmark as baseline  # noqa: E402
from app.auth.service_jwt import (  # noqa: E402
    DEFAULT_AUDIENCE,
    DEFAULT_ISSUER,
    DEFAULT_REQUIRED_SCOPE,
)
from app.main import app  # noqa: E402
from tests.test_frangi_sprint3 import (  # noqa: E402
    EXPECTED_CLIENT_ID,
    VALID_SUB,
    generate_deterministic_rsa_keypair,
    sign_rs256_jwt,
)

CONTRACTS_DIR = REPO_ROOT / "contracts"
OUTPUT_REPORT = REPO_ROOT / "docs" / "benchmarks" / "frangi_runtime_api_v1.2.json"


def deterministic_uuid_for_stem(stem: str) -> str:
    digest = hashlib.sha256(f"aura-s3-benchmark:{stem}".encode("utf-8")).hexdigest()
    return f"{digest[:8]}-{digest[8:12]}-4{digest[13:16]}-8{digest[17:20]}-{digest[20:32]}"


def main() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    req_schema = json.loads((CONTRACTS_DIR / "ai-request-1.2.schema.json").read_text(encoding="utf-8"))
    resp_schema = json.loads((CONTRACTS_DIR / "ai-response-1.2.schema.json").read_text(encoding="utf-8"))
    req_validator = jsonschema.Draft202012Validator(req_schema, format_checker=jsonschema.FormatChecker())
    resp_validator = jsonschema.Draft202012Validator(resp_schema, format_checker=jsonschema.FormatChecker())

    keypair = generate_deterministic_rsa_keypair(seed=20261028, bits=2048)
    os.environ["AURA_AI_ENGINE"] = "frangi"
    os.environ["AURA_ALLOW_MOCK"] = "false"
    os.environ["AURA_SERVICE_AUTH_PUBLIC_KEY_PEM"] = keypair.public_pem
    os.environ.pop("AURA_SERVICE_AUTH_PUBLIC_KEY_PATH", None)

    now = int(time.time())
    token = sign_rs256_jwt(
        keypair,
        header={"alg": "RS256", "typ": "JWT", "kid": keypair.key_id},
        payload={
            "iss": DEFAULT_ISSUER,
            "aud": DEFAULT_AUDIENCE,
            "sub": VALID_SUB,
            "client_id": EXPECTED_CLIENT_ID,
            "scope": DEFAULT_REQUIRED_SCOPE,
            "iat": now,
            "nbf": now,
            "exp": now + 300,
        },
    )
    headers = {"Authorization": f"Bearer {token}"}

    with baseline.MANIFEST.open(encoding="utf-8-sig", newline="") as f:
        rows = [r for r in csv.DictReader(f) if r["split"] == "test"]

    if len(rows) != 6:
        raise ValueError(f"Expected 6 test rows, got {len(rows)}")

    samples: List[Dict[str, Any]] = []

    with TestClient(app) as client:
        ready_resp = client.get("/ready")
        if ready_resp.status_code != 200:
            raise RuntimeError(f"/ready failed with {ready_resp.status_code}: {ready_resp.text}")

        for row in rows:
            image_path = baseline.read_verified(row, "image")
            mask_path = baseline.read_verified(row, "mask_1st")

            raw_bytes = image_path.read_bytes()
            eye_side = "left" if row["eye_side"].upper() == "L" else "right"
            req_id = deterministic_uuid_for_stem(image_path.stem)

            payload = {
                "request_id": req_id,
                "patient_id": None,
                "eye_side": eye_side,
                "mode": "retina_vessels",
                "modality": "FUNDUS",
                "image_base64": base64.b64encode(raw_bytes).decode("ascii"),
                "include_mask": True,
                "include_overlay": True,
            }
            req_validator.validate(payload)

            t0 = time.perf_counter()
            http_resp = client.post("/api/v1/analyze", json=payload, headers=headers)
            wall_ms = round((time.perf_counter() - t0) * 1000.0, 2)

            if http_resp.status_code != 200:
                raise RuntimeError(f"POST /api/v1/analyze failed for {image_path.name}: {http_resp.text}")

            body = http_resp.json()
            resp_validator.validate(body)

            mask_bytes = base64.b64decode(body["segmentation"]["mask_base64"], validate=True)
            with Image.open(io.BytesIO(mask_bytes)) as pred_img:
                pred_arr = np.asarray(pred_img.convert("L"), dtype=np.uint8) > 0

            with Image.open(mask_path) as gt_img:
                gt_arr = np.asarray(gt_img.convert("L"), dtype=np.uint8) > 0

            overlap = baseline.overlap_metrics(pred_arr, gt_arr)
            samples.append(
                {
                    "image": image_path.name,
                    "subject_id": row["subject_id"],
                    "eye_side": eye_side,
                    "request_id": req_id,
                    "status": body["status"],
                    "is_mock": body["is_mock"],
                    "risk_available": body["risk_available"],
                    "risk_assessment": body["risk_assessment"],
                    "vessel_density": body["metrics"]["vessel_density"],
                    "dice": overlap["dice"],
                    "iou": overlap["iou"],
                    "sensitivity": overlap["sensitivity"],
                    "specificity": overlap["specificity"],
                    "processing_time_ms": body["processing_time_ms"],
                    "http_roundtrip_ms": wall_ms,
                }
            )

    proc_times = [s["processing_time_ms"] for s in samples]
    http_times = [s["http_roundtrip_ms"] for s in samples]

    report: Dict[str, Any] = {
        "status": "SPRINT3_RUNTIME_API_BENCHMARK_VERIFIED",
        "timestamp_utc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "api_version": "1.2.0",
        "model_version": "frangi-v0.2",
        "config_version": "s3-frangi-1.0.0",
        "threshold_version": "frangi-threshold-0.02-v1",
        "is_mock": False,
        "risk_available": False,
        "jwt_auth_enforced": True,
        "dataset": "CHASE_DB1",
        "split": "test",
        "sample_count": len(samples),
        "subject_count": 3,
        "geometry_preserved": [999, 960],
        "environment": {
            "os": platform.platform(),
            "python": platform.python_version(),
            "Pillow": PIL.__version__,
            "numpy": np.__version__,
            "scipy": scipy.__version__,
            "scikit_image": skimage.__version__,
        },
        "accuracy_metrics": {
            "mean_dice": round(float(np.mean([s["dice"] for s in samples])), 6),
            "mean_iou": round(float(np.mean([s["iou"] for s in samples])), 6),
            "mean_sensitivity": round(float(np.mean([s["sensitivity"] for s in samples])), 6),
            "mean_specificity": round(float(np.mean([s["specificity"] for s in samples])), 6),
        },
        "runtime_latency_ms": {
            "scope": "in_process_fastapi_endpoint_including_jwt_decode_quality_frangi_png_encode",
            "excludes": [
                "network_transport",
                "blob_storage_download_upload",
                "rabbitmq_queue_transit",
                "postgres_outbox_persistence",
            ],
            "processing_time_median_ms": round(float(np.median(proc_times)), 2),
            "processing_time_p95_ms": round(float(np.percentile(proc_times, 95)), 2),
            "processing_time_max_ms": round(float(np.max(proc_times)), 2),
            "http_roundtrip_median_ms": round(float(np.median(http_times)), 2),
            "http_roundtrip_p95_ms": round(float(np.percentile(http_times, 95)), 2),
            "http_roundtrip_max_ms": round(float(np.max(http_times)), 2),
        },
        "samples": samples,
        "limitations": (
            "Component-level AI Core API benchmark on 6 test images (999x960) from CHASE_DB1. "
            "Does not certify end-to-end Worker/RabbitMQ/Blob/Database NFR latency or clinical diagnostic efficacy."
        ),
    }

    OUTPUT_REPORT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_REPORT.write_text(
        json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
