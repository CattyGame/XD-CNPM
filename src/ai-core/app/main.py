"""
AURA AI Core: platform endpoints, RS256 service JWT authentication,
real Frangi v0.2 retinal vessel segmentation API (Sprint 3 Contract 1.2.0),
and explicit opt-in mock mode for unit regression testing.
"""

from __future__ import annotations

import logging
import os
import time
import uuid
from typing import Optional

from fastapi import Depends, FastAPI, Header, HTTPException, Request, Security, status
from fastapi.encoders import jsonable_encoder
from fastapi.exceptions import RequestValidationError
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

from .auth.service_jwt import (
    ServiceAuthConfigError,
    ServiceAuthError,
    load_verifier_from_env,
)
from .schemas.analysis import (
    AnalysisRequest,
    AnalysisResponse,
    ErrorResponse,
)
from .schemas.common import AnalysisMode
from .schemas.health import HealthResponse, PingResponse, ReadyResponse
from .services.frangi_service import (
    FrangiConfigError,
    FrangiRuntimeConfig,
    LowQualityImageError,
     SUPPORTED_FRANGI_FORMATS,
    load_frangi_config,
    process_frangi_analysis,
    utc_timestamp_z,
)
from .services.mock_service import (
    InvalidImagePayloadError,
    MockLowQualityImageError,
    process_mock_analysis,
)

APP_START_TIME = time.time()
SERVICE_NAME = "AURA.AiCore"
VERSION = "0.2.0"
API_VERSION = "1.2.0"

logger = logging.getLogger(__name__)

service_bearer_scheme = HTTPBearer(
    scheme_name="serviceBearer",
    bearerFormat="JWT",
    description="RS256 service token issued by Identity (`aura-identity-s3-v1`) with audience `aura-ai` and scope `ai:analyze`.",
    auto_error=False,
)

app = FastAPI(
    title="AURA AI Analysis Service API",
    description=(
        "AI API nội bộ của AURA (Contract 1.2.0). Runtime mặc định Sprint 3 chạy "
        "thuật toán phân vùng mạch máu võng mạc Frangi v0.2 thật theo cấu hình cố định "
        "trong config/ai-frangi.yaml và xác thực RS256 Service JWT fail-closed. "
        "Phân vùng Frangi chưa cung cấp đánh giá rủi ro lâm sàng (risk_available=false, "
        "risk_assessment=null, status=WARNING kèm RISK_UNAVAILABLE)."
    ),
    version=API_VERSION,
    contact={
        "name": "Dao Duy Quan (M4 Lead)",
        "email": "quandd2937@ut.edu.vn",
    },
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


def is_explicit_mock_mode() -> bool:
    """
    Return True only when both AURA_AI_ENGINE=mock and AURA_ALLOW_MOCK=true
    are explicitly set for unit/scenario regression tests.
    Default Sprint 3 runtime is real Frangi mode (allowMock=false).
    """
    engine = os.getenv("AURA_AI_ENGINE", "frangi").strip().lower()
    allow_mock = os.getenv("AURA_ALLOW_MOCK", "false").strip().lower() in (
        "1",
        "true",
        "yes",
    )
    return engine == "mock" and allow_mock


def verify_runtime_and_auth(
    request: Request,
    credentials: Optional[HTTPAuthorizationCredentials] = Security(service_bearer_scheme),
    authorization: Optional[str] = Header(default=None, alias="Authorization"),
) -> Optional[FrangiRuntimeConfig]:
    """
    FastAPI dependency executed before request body validation on POST /api/v1/analyze:
    - In explicit mock mode (unit regression only), skips JWT & Frangi config checks.
    - In default Sprint 3 Frangi mode, enforces fail-closed readiness (503) and
      RS256 service JWT authentication (401) + authorization (403).
    """
    if is_explicit_mock_mode():
        return None

    configured_engine = os.getenv("AURA_AI_ENGINE", "frangi").strip().lower()
    if configured_engine != "frangi":
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail={
                "error_code": "SERVICE_NOT_READY",
                "message": (
                    f"AI engine '{configured_engine}' không khả dụng hoặc mock mode "
                    "bị khoá (allowMock=false) trong Sprint 3."
                ),
                "details": {"engine": configured_engine},
            },
        )

    try:
        frangi_config = load_frangi_config()
    except FrangiConfigError as error:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail={
                "error_code": "SERVICE_NOT_READY",
                "message": f"Cấu hình Frangi chưa sẵn sàng: {error}",
                "details": None,
            },
        ) from error

    try:
        verifier = load_verifier_from_env()
    except ServiceAuthConfigError as error:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail={
                "error_code": "SERVICE_NOT_READY",
                "message": str(error),
                "details": None,
            },
        ) from error

    raw_auth_header = authorization or request.headers.get("Authorization")
    if not raw_auth_header and credentials is not None:
        raw_auth_header = f"{credentials.scheme} {credentials.credentials}"

    try:
        verifier.verify_authorization_header(raw_auth_header)
    except ServiceAuthError as error:
        headers = {"WWW-Authenticate": "Bearer"} if error.status_code == 401 else None
        raise HTTPException(
            status_code=error.status_code,
            detail={
                "error_code": error.error_code,
                "message": error.message,
                "details": error.details,
            },
            headers=headers,
        ) from error

    return frangi_config


@app.exception_handler(RequestValidationError)
async def validation_exception_handler(
    request: Request,
    exc: RequestValidationError,
):
    errors = exc.errors()

    for error in errors:
        if "mode" in error.get("loc", ()):
            return JSONResponse(
                status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                content={
                    "error_code": "UNSUPPORTED_MODE",
                    "message": (
                        "Chế độ phân tích không được hỗ trợ; "
                        "chỉ chấp nhận retina_vessels."
                    ),
                    "details": {
                        "supported_modes": ["retina_vessels"],
                        "received_mode": str(error.get("input", "")),
                        "errors": jsonable_encoder(errors),
                    },
                    "timestamp": utc_timestamp_z(),
                },
            )

    return JSONResponse(
        status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
        content={
            "error_code": "SCHEMA_VALIDATION_ERROR",
            "message": (
                "Dữ liệu không đúng định dạng hoặc thiếu trường bắt buộc."
            ),
            "details": {"errors": jsonable_encoder(errors)},
            "timestamp": utc_timestamp_z(),
        },
    )


@app.exception_handler(HTTPException)
async def http_exception_handler(request: Request, exc: HTTPException):
    error_code = f"HTTP_{exc.status_code}"
    message = "Lỗi xử lý yêu cầu."
    details = None

    if isinstance(exc.detail, dict):
        error_code = exc.detail.get("error_code", error_code)
        message = exc.detail.get("message", message)
        details = exc.detail.get("details")
    elif isinstance(exc.detail, str):
        message = exc.detail
        if "unsupported_modality" in message.lower():
            error_code = "UNSUPPORTED_MODALITY"
        elif "invalid_image_payload" in message.lower():
            error_code = "INVALID_IMAGE_PAYLOAD"

    return JSONResponse(
        status_code=exc.status_code,
        content={
            "error_code": error_code,
            "message": message,
            "details": details,
            "timestamp": utc_timestamp_z(),
        },
        headers=exc.headers,
    )


@app.get("/", include_in_schema=False)
def root():
    return {
        "service": SERVICE_NAME,
        "version": VERSION,
        "api_version": API_VERSION,
        "status": "running",
        "health": "/health",
        "ready": "/ready",
        "ping": "/api/v1/platform/ping",
        "analyze": "/api/v1/analyze",
    }


@app.get("/health", response_model=HealthResponse, tags=["Health"])
def health():
    """Liveness check only."""
    return HealthResponse(
        status="ok",
        version= "0.1.0",
        service=SERVICE_NAME,
        device="cpu",
        model_loaded=False,
        uptime_seconds=round(time.time() - APP_START_TIME, 2),
    )


@app.get(
    "/ready",
    response_model=ReadyResponse,
    responses={
        503: {
            "model": ErrorResponse,
            "description": "Not ready (missing/invalid Frangi config, missing/invalid JWT public key, or running in mock mode).",
        },
    },
    tags=["Health"],
)
def ready():
    """
    Readiness probe for Sprint 3: returns HTTP 200 only when real Frangi configuration
    and RS256 service JWT public key are loaded and validated; returns 503 otherwise.
    """
    configured_engine = os.getenv("AURA_AI_ENGINE", "frangi").strip().lower()
    if is_explicit_mock_mode() or configured_engine != "frangi":
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail={
                "error_code": "SERVICE_NOT_READY",
                "message": "AI Core đang ở chế độ mock hoặc chưa bật engine Frangi thật.",
                "details": {
                    "engine": configured_engine,
                    "is_mock_mode": is_explicit_mock_mode(),
                },
            },
        )

    try:
        frangi_config = load_frangi_config()
    except FrangiConfigError as error:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail={
                "error_code": "SERVICE_NOT_READY",
                "message": f"Cấu hình Frangi chưa sẵn sàng: {error}",
                "details": None,
            },
        ) from error

    try:
        load_verifier_from_env()
    except ServiceAuthConfigError as error:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail={
                "error_code": "SERVICE_NOT_READY",
                "message": str(error),
                "details": None,
            },
        ) from error

    return ReadyResponse(
        status="ready",
        service=SERVICE_NAME,
        version=VERSION,
        api_version=API_VERSION,
        engine=frangi_config.engine,
        model_version=frangi_config.model_version,
        config_version=frangi_config.config_version,
        threshold_version=frangi_config.threshold_version,
        jwt_verification_ready=True,
        timestamp=utc_timestamp_z(),
    )


@app.get(
    "/api/v1/platform/ping",
    response_model=PingResponse,
    tags=["Platform"],
)
def ping():
    return PingResponse(
        service=SERVICE_NAME,
        message="AI Core template is running",
        timestamp=utc_timestamp_z(),
    )


@app.post(
    "/api/v1/analyze",
    operation_id="analyzeRetina",
    response_model=AnalysisResponse,
    responses={
        400: {
            "model": ErrorResponse,
            "description": "Invalid Base64/image payload",
        },
        401: {
            "model": ErrorResponse,
            "description": "Invalid token",
        },
        403: {
            "model": ErrorResponse,
            "description": "Wrong audience/scope",
        },
        422: {
            "model": ErrorResponse,
            "description": "LOW_QUALITY_IMAGE or unsupported input",
        },
        500: {
            "model": ErrorResponse,
            "description": "Processing failure",
        },
        503: {
            "model": ErrorResponse,
            "description": "Not ready",
        },
    },
    tags=["Analysis"],
)
def analyze_retina(
    request: AnalysisRequest,
    frangi_config: Optional[FrangiRuntimeConfig] = Depends(verify_runtime_and_auth),
):
    """
    API nội bộ nhận ảnh FUNDUS Base64 và thực thi phân vùng mạch máu võng mạc Frangi v0.2 thật.

    - Yêu cầu xác thực RS256 Service JWT (`iss=aura-identity-s3-v1`, `aud=aura-ai`, `scope=ai:analyze`).
    - Giữ nguyên kích thước ảnh gốc (không crop/resize 512), trả về PNG mask (`L`, `0/255`) và PNG overlay (`RGB`).
    - Trả về `status=WARNING` kèm `RISK_UNAVAILABLE`, `is_mock=false`, `risk_available=false`, `risk_assessment=null`.
    - Từ chối ảnh kém chất lượng với HTTP 422 `LOW_QUALITY_IMAGE`.
    """
    if not request.image_base64 or not request.image_base64.strip():
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail={
                "error_code": "INVALID_IMAGE_PAYLOAD",
                "message": "Chuỗi image_base64 không được để trống.",
                "details": None,
            },
        )

    norm_modality = (
        request.modality.strip().upper()
        if request.modality else "FUNDUS"
    )

    if norm_modality == "OCT":
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail={
                "error_code": "UNSUPPORTED_MODALITY",
                "message": "OCT chưa được hỗ trợ; chỉ chấp nhận FUNDUS.",
                "details": {
                    "supported_modalities": ["FUNDUS"],
                    "received_modality": request.modality,
                },
            },
        )

    if norm_modality != "FUNDUS":
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail={
                "error_code": "INVALID_MODALITY",
                "message": "Modality không hợp lệ; chỉ chấp nhận FUNDUS.",
                "details": {
                    "supported_modalities": ["FUNDUS"],
                    "received_modality": request.modality,
                },
            },
        )

    if request.mode != AnalysisMode.RETINA_VESSELS:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail={
                "error_code": "UNSUPPORTED_MODE",
                "message": "Chỉ hỗ trợ chế độ retina_vessels.",
                "details": {
                    "supported_modes": ["retina_vessels"],
                    "received_mode": request.mode.value,
                },
            },
        )

    # Explicit mock mode for unit/scenario regression tests only
    if is_explicit_mock_mode():
        try:
            return process_mock_analysis(request)
        except InvalidImagePayloadError as error:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail={
                    "error_code": "INVALID_IMAGE_PAYLOAD",
                    "message": str(error),
                    "details": {
                        "supported_formats": ["PNG", "JPEG", "WEBP"],
                    },
                },
            ) from error
        except MockLowQualityImageError as error:
            raise HTTPException(
                status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                detail={
                    "error_code": "LOW_QUALITY_IMAGE",
                    "message": str(error),
                    "details": {
                        "is_mock": True,
                        "reason": "MOCK_LOW_QUALITY",
                        "quality_assessment_performed": False,
                    },
                },
            ) from error
        except HTTPException:
            raise
        except Exception as error:
            logger.exception("Mock AI analysis processing failed.")
            raise HTTPException(
                status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
                detail={
                    "error_code": "INFERENCE_RUNTIME_ERROR",
                    "message": "Lỗi nội bộ khi xử lý AI.",
                    "details": None,
                },
            ) from error

    # Sprint 3 Contract 1.2 strict request invariants in real Frangi mode
    try:
        uuid.UUID(str(request.request_id))
    except ValueError as error:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail={
                "error_code": "SCHEMA_VALIDATION_ERROR",
                "message": "request_id phải là chuỗi UUID hợp lệ theo schema 1.2.",
                "details": {"field": "request_id", "received": request.request_id},
            },
        ) from error

    if request.patient_id is not None:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail={
                "error_code": "SCHEMA_VALIDATION_ERROR",
                "message": "patient_id bắt buộc là null trong AI request contract 1.2.",
                "details": {"field": "patient_id"},
            },
        )

    if not request.include_mask or not request.include_overlay:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail={
                "error_code": "SCHEMA_VALIDATION_ERROR",
                "message": "include_mask và include_overlay bắt buộc đều là true trong Sprint 3.",
                "details": {
                    "include_mask": request.include_mask,
                    "include_overlay": request.include_overlay,
                },
            },
        )

    try:
        return process_frangi_analysis(request, config=frangi_config)

    except InvalidImagePayloadError as error:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail={
                "error_code": "INVALID_IMAGE_PAYLOAD",
                "message": str(error),
                "details": {
                    "supported_formats": list(SUPPORTED_FRANGI_FORMATS),
                },
            },
        ) from error

    except LowQualityImageError as error:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail={
                "error_code": "LOW_QUALITY_IMAGE",
                "message": str(error),
                "details": error.report.to_details_dict(),
            },
        ) from error

    except HTTPException:
        raise

    except Exception as error:
        logger.exception("Real Frangi AI analysis processing failed.")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail={
                "error_code": "INFERENCE_RUNTIME_ERROR",
                "message": "Lỗi nội bộ khi xử lý phân vùng mạch máu Frangi.",
                "details": None,
            },
        ) from error
