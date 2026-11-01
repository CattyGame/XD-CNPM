# Phiếu Thẩm định Kỹ thuật & Yêu cầu Bổ sung: SCRUM-325 (W3-M2-03)

> **Mã công việc Jira**: **SCRUM-325** (`[W3]M2] Cung cấp API tiến trình và kết quả có kiểm tra quyền`)  
> **Người thực hiện**: **Võ Hữu Duy (M2 — Outbox Publisher & Analysis Worker)**  
> **Người thẩm định**: **Nguyễn Trương Hậu (M1 — Lead Architect / DevOps)**  
> **Ngày thẩm định**: 30/10/2026  
> **Trạng thái Thẩm định**: ⚠️ **CHANGES REQUESTED (Yêu cầu bổ sung endpoint trước khi chuyển sang DONE)**  
> **Tài liệu tham chiếu**:
> * Hợp đồng API chuẩn: `infra/sprint3/contracts/analysis-openapi-0.7.yaml` (dòng 142–178)
> * Báo cáo phụ thuộc tích hợp của Frontend M5: `docs/api/SCRUM-333-result-inbox-api-mapping.md`

---

## I. Tổng quan Kết quả Thẩm định

### 1. Các phần ĐÃ HOÀN THÀNH XUẤT SẮC (PASS)
* **`GET /api/v1/batches/{batchId}`**:
  * Hiện thực chuẩn xác qua `BatchStatusService.cs` và `PostgresBatchStatusRepository.cs`.
  * Cơ chế Polling an toàn tuyệt đối: không tạo upload, không sinh duplicate job, không gọi trừ billing credit.
  * Quy tắc tính trạng thái batch tuân thủ nghiêm ngặt: HTTP 202/receipt được coi là `IN_PROGRESS`, không bao giờ đánh dấu `COMPLETED` sớm.
  * Đạt **12/12 Unit Tests** trong `BatchStatusTests.cs`.
* **`GET /api/v1/analyses/{analysisId}/files/{fileId}/access`**:
  * Hiện thực an toàn qua `FileAccessService.cs` và `PostgresFileAccessRepository.cs`.
  * Kiểm quyền đa tenant với Profile API (`action: FILE_READ`) trước khi cấp URL đọc ảnh gốc (`aura-originals`) hoặc mask/overlay (`aura-annotations`).
  * Trả SAS token ngắn hạn (TTL 120s), header `Cache-Control: no-store`, trả mã lỗi `404 ERR_NOT_FOUND` nhất quán khi tài nguyên không tồn tại hoặc không có quyền (tuân thủ quy tắc bảo mật BR-02).
  * Đạt **8/8 Unit Tests** trong `FileAccessTests.cs`.

---

### 2. Vấn đề Phát hiện (Missing Endpoint Blocker)
Khi đối chiếu với hợp đồng **OpenAPI 0.7** (`infra/sprint3/contracts/analysis-openapi-0.7.yaml`) và phần Frontend của M5 (`src/web/aura-web/src/api/analysisResult.ts`), phân hệ Analysis API hiện đang **THIẾU ROUTE**:

$$\mathbf{GET\ /api/v1/analyses/\{analysisId\}}$$

* **Thực trạng**: Trong file `src/services/AURA.Analysis.Api/Http/AnalysisEndpoints.cs`, nhóm chỉ mới đăng ký 2 routes:
  1. `POST /api/v1/analyses` (Upload)
  2. `GET /api/v1/batches/{batchId}` (Batch status)
  3. `GET /api/v1/analyses/{analysisId}/files/{fileId}/access` (Cấp URL ký ảnh)
* **Hậu quả**: Khi bác sĩ hoặc bệnh nhân từ màn hình **Hộp thư thông báo (Inbox)** hoặc **Tiến trình Batch** bấm vào liên kết *"Xem kết quả chi tiết"* (`/analyses/:analysisId`), Frontend M5 gọi `GET /api/v1/analyses/{analysisId}` và nhận về lỗi **`404 Route Not Found`**.
* Do đó, M5 hiện đang bị chặn (Blocker), chưa thể hoàn tất kiểm thử E2E cho ticket **SCRUM-333** (xem xác nhận tại `docs/api/SCRUM-333-result-inbox-api-mapping.md`, Mục 6.2).

---

## II. Đặc tả Kỹ thuật Endpoint Cần Bổ sung

### 1. Thông tin Route
* **Method**: `GET`
* **Path**: `/api/v1/analyses/{analysisId}`
* **Headers yêu cầu**:
  * `Authorization: Bearer <Firebase ID Token>` (Bắt buộc)
  * `X-Correlation-Id: <string>` (Tuỳ chọn, độ dài 1–64)
* **Query Parameters**:
  * `clinicContextId` (UUID, tuỳ chọn)
* **Response Header**: `Cache-Control: no-store`

### 2. Ma trận Phân quyền & Bảo mật (Theo BR-02 & M6 Contract)
1. Xác thực người dùng qua `IAnalysisUserIdentityResolver`.
2. Kiểm tra vai trò lâm sàng hợp lệ (`PATIENT`, `DOCTOR`, `CLINIC_ADMIN`).
3. Truy vấn metadata của `analysisId` từ database (lấy `tenantId`, `patientId`, `ownerId`).
   * Nếu không tìm thấy record $\rightarrow$ Trả về `404 ERR_NOT_FOUND`.
4. Gọi `IPatientAccessResolver.ResolveAsync` sang Profile API với:
   * `Action`: `"ANALYSIS_READ"`
   * `PatientId`: `patientId` của analysis
   * `RequesterId`: `accountId` từ token
   * `ClinicId`: `clinicContextId` (nếu có)
5. **Kiểm tra kết quả quyền**:
   * Nếu `!decision.Allowed` HOẶC `decision.TenantId != record.TenantId` HOẶC `decision.OwnerPrincipalId != record.OwnerId`:
     $\rightarrow$ Ném `UploadFailure(404, "ERR_NOT_FOUND", "Không tìm thấy tài nguyên.")`.  
     *(Tuyệt đối không trả 403 để tránh rò rỉ sự tồn tại của dữ liệu giữa các tenant khác nhau).*

---

### 3. Cấu trúc Response JSON Chuẩn (HTTP 200 OK)

Phản hồi phải tuân thủ schema `Analysis` trong OpenAPI 0.7 và khớp với hàm `parseAnalysis` tại `src/web/aura-web/src/api/analysisResult.ts`:

#### A. Khi trạng thái là `COMPLETED` (Đã có kết quả AI)
```json
{
  "analysisId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "batchId": "7d9e4c12-3456-4abc-9def-0123456789ab",
  "patientId": "12345678-1234-1234-1234-123456789abc",
  "processingStatus": "COMPLETED",
  "reviewStatus": "NOT_REQUIRED",
  "createdAt": "2026-10-30T12:00:00Z",
  "updatedAt": "2026-10-30T12:00:15Z",
  "image": {
    "fileId": "a1b2c3d4-e5f6-47a8-b9c0-d1e2f3a4b5c6",
    "kind": "original"
  },
  "result": {
    "version": 1,
    "isMock": false,
    "riskAvailable": false,
    "riskLevel": null,
    "riskScore": null,
    "confidenceScore": null,
    "modelVersion": "frangi-v0.2",
    "configVersion": "s3-frangi-1.0.0",
    "thresholdVersion": "frangi-threshold-0.02-v1",
    "disclaimer": "Kết quả phân vùng nghiên cứu, không phải chẩn đoán.",
    "limitations": "Segmentation research baseline",
    "metrics": {
      "vesselDensity": 0.08,
      "tortuosityIndex": null,
      "avRatio": null,
      "fractalDimension": null,
      "branchingPoints": null
    },
    "indicators": [],
    "annotations": [
      {
        "fileId": "e1f2a3b4-c5d6-47e8-90ab-cdef12345678",
        "kind": "mask"
      },
      {
        "fileId": "f2a3b4c5-d6e7-48f9-01bc-def123456789",
        "kind": "overlay"
      }
    ],
    "warnings": [
      {
        "code": "RISK_UNAVAILABLE",
        "message": "Frangi provides vessel segmentation only."
      }
    ]
  },
  "error": null
}
```

#### B. Khi trạng thái đang xử lý (`PENDING`, `QUEUED`, `PROCESSING`, `RETRY_READY`)
* Trường `result` bắt buộc là `null`.
* Trường `error` là `null`.

#### C. Khi trạng thái kết thúc có lỗi (`FAILED`, `LOW_QUALITY`, `REJECTED`, `DUPLICATE`)
* Trường `result` là `null`.
* Trường `error` chứa thông tin lỗi:
  ```json
  "error": {
    "code": "ERR_IMAGE_LOW_QUALITY",
    "message": "Chất lượng ảnh chưa đạt yêu cầu.",
    "retryable": false
  }
  ```

---

## III. Hướng dẫn Hiện thực Chi tiết

M2 có thể triển khai theo kiến trúc sẵn có của `AURA.Analysis.Api` như sau:

### Bước 1: Tạo Repository & Service
Tạo mới file `src/services/AURA.Analysis.Api/Upload/AnalysisDetailService.cs`:
* Tạo interface `IAnalysisDetailRepository`:
  ```csharp
  public sealed record AnalysisDetailRecord(
      Guid AnalysisId, Guid BatchId, Guid TenantId, Guid PatientId, Guid OwnerId,
      string ProcessingStatus, string ReviewStatus, DateTime CreatedAt, DateTime UpdatedAt,
      Guid OriginalFileId, string? ResultJson, string? ErrorCode);

  public interface IAnalysisDetailRepository
  {
      Task<AnalysisDetailRecord?> FindAsync(Guid analysisId, CancellationToken ct);
  }
  ```
* Tạo service `AnalysisDetailService`:
  * Nhận `IAnalysisDetailRepository` và `IPatientAccessResolver`.
  * Thực hiện kiểm quyền `ANALYSIS_READ`.
  * Parse trường `ResultJson` (do Worker lưu từ `SCRUM-324`) thành DTO `AnalysisResult`.
  * Trả về model DTO khớp với schema JSON ở trên.

### Bước 2: Tạo Repository Postgres
Tạo file `src/services/AURA.Analysis.Api/Upload/PostgresAnalysisDetailRepository.cs`:
* Viết SQL truy vấn lấy thông tin từ bảng `analysis.images i`:
  * `JOIN analysis.analysis_requests r ON r.tenant_id = i.tenant_id AND r.id = i.analysis_request_id`
  * `LEFT JOIN LATERAL (...) ar ON true` để lấy `result_json` từ bảng `analysis.analysis_results` (phiên bản mới nhất).
  * `LEFT JOIN LATERAL (...) aj ON true` để lấy `error_code` từ bảng `analysis.jobs`.

### Bước 3: Đăng ký Endpoint và DI
* Trong `src/services/AURA.Analysis.Api/Http/AnalysisEndpoints.cs`:
  ```csharp
  app.MapGet("/api/v1/analyses/{analysisId}", (HttpContext c, string analysisId) => Handle(c, async () =>
  {
      var user = await User(c);
      var clinic = Clinic(c);
      var service = c.RequestServices.GetRequiredService<AnalysisDetailService>();
      var result = await service.GetAsync(user.AccountId, clinic, ResourceId(analysisId), c.RequestAborted);
      return Results.Json(result, UploadJson.Options);
  }));
  ```
* Trong `src/services/AURA.Analysis.Api/Http/AnalysisRuntime.cs`:
  * Đăng ký `services.AddSingleton<IAnalysisDetailRepository, PostgresAnalysisDetailRepository>();`
  * Đăng ký `services.AddTransient<AnalysisDetailService>();`

### Bước 4: Viết Unit Tests
Tạo file `src/services/AURA.Analysis.Api.Tests/AnalysisDetailTests.cs`:
* `Get_WithPermission_ReturnsCompletedResult_WithAnnotationsAndCamelCase`
* `Get_DeniedOrCrossTenant_Returns404NotFound`
* `Get_ProcessingStatusNotCompleted_ReturnsNullResult`
* `Get_FailedOrLowQuality_ReturnsErrorPayload`

---

## IV. Mẫu Bình luận (Comment) gửi M2 trên Jira Ticket SCRUM-325

Bạn có thể sao chép đoạn văn dưới đây để dán vào phần bình luận của ticket **SCRUM-325** trên Jira:

```markdown
Chào Duy @duyvh1011,

Mình đã review mã nguồn và kiểm thử toàn bộ kết quả của task **SCRUM-325** trên nhánh develop:
1. Endpoint `GET /api/v1/batches/{batchId}` và `GET /api/v1/analyses/{analysisId}/files/{fileId}/access` đã đạt 100% yêu cầu (20/20 testcases PASS, cơ chế kiểm quyền Profile API và cấp SAS URL hoạt động rất tốt).
2. Tuy nhiên, theo hợp đồng OpenAPI 0.7 (`infra/sprint3/contracts/analysis-openapi-0.7.yaml` dòng 142) và yêu cầu tích hợp trang Result của M5 (`AnalysisResultPage.tsx` / SCRUM-333), hệ thống hiện đang thiếu endpoint:
   👉 `GET /api/v1/analyses/{analysisId}`
   Hiện tại nếu gọi endpoint này sẽ bị lỗi 404 Route Not Found, khiến M5 bị nghẽn (block) chưa thể test E2E màn hình hiển thị kết quả phân tích chi tiết.

Mình đã soạn sẵn tài liệu đặc tả chi tiết kèm hướng dẫn code từng bước tại:
📄 `docs/qa/m2_sprint3_scrum325_review_and_action_items.md`

Nhờ Duy bổ sung nhanh route này kèm unit test tương ứng để M5 tích hợp xong là chúng ta có thể chính thức đóng DONE cả SCRUM-325 và SCRUM-333 nhé. Cảm ơn Duy!
```
