# Khảo sát Kỹ thuật & Đánh giá Tính khả thi Định dạng Ảnh Cắt lớp Võng mạc OCT (v1.0)

> **Dự án**: AURA — Hệ thống Phân tích Hình ảnh Võng mạc Hỗ trợ Sàng lọc Sức khỏe Mạch máu
> **Mã công việc**: `SCRUM-352` (`W45-M4-03` — Sprint 4–5) · **Epic**: `E5 AI Core (SCRUM-265)`
> **Tác giả**: Đào Duy Quân (`M4 — AI Core Lead`)
> **Phối hợp kiến trúc**: `M1` (Gateway / Storage / Infra), `M2` (Analysis API & Worker), `M3` (Billing Credit Policy)
> **Phiên bản**: `1.0.0` · **Ngày lập**: `2026-10-30`
> **Trạng thái**: Báo cáo khảo sát khả thi kỹ thuật (Technical Feasibility Study) & Kế hoạch nền tảng cho Sprint 6. Trong Sprint 4–5 (AI Contract `1.3.0`), `modality="OCT"` tiếp tục được từ chối an toàn theo cơ chế fail-closed với `HTTP 422 UNSUPPORTED_MODALITY`.

---

## 1. Mục tiêu Khảo sát & Tuyên bố Minh bạch Học thuật

Tài liệu này mở rộng bản rà soát sơ bộ Sprint 2 (`docs/ai/feasibility_risk_model_and_oct_v1.0.md`) nhằm thiết lập hồ sơ kỹ thuật đầy đủ cho việc mở rộng AURA sang phương thức chụp cắt lớp quang học võng mạc (**OCT — Optical Coherence Tomography**), bao gồm:

1. Phân tích cấu trúc dữ liệu ảnh cắt lớp OCT (`DICOM Ophthalmic Tomography`, `Multi-page TIFF`, và chuỗi `Lossless PNG B-scan`).
2. So sánh kiến trúc mô hình học sâu ứng viên (**2D / 2.5D ResNet-FPN** so với **3D U-Net**) cùng ước tính ngân sách phần cứng (`GPU / VRAM / CPU / RAM / I/O`).
3. Thiết kế phương án tiếp nhận dữ liệu (**Claim-Check / Private Object Storage Ingestion** kèm khử định danh `PHI`) không phá vỡ hợp đồng `POST /api/v1/analyze` hiện hữu.
4. Chốt phạm vi triển khai thực tế và lộ trình kỹ thuật cho **Sprint 6**.

> **Lưu ý trung thực học thuật**: Hiện tại kho mã nguồn AURA mới xác minh trực tiếp bộ dữ liệu đáy mắt màu 2D `CHASE_DB1` (`FUNDUS`, `999x960 RGB`). Các thông số bộ nhớ GPU/VRAM và kích thước tensor dưới đây là **tính toán giải tích kích thước bộ nhớ & tham chiếu kiến trúc chuẩn**, chưa phải kết quả đo benchmark thực nghiệm của AURA trên tập dữ liệu OCT lâm sàng. Mọi tuyên bố hiệu năng OCT thực tế bắt buộc phải được đo lại sau khi chốt dataset, giấy phép và phần cứng ở Sprint 6.

---

## 2. Phân tích Cấu trúc Dữ liệu Ảnh Cắt lớp Võng mạc OCT

Khác với ảnh chụp đáy mắt màu 2D (`Color Fundus Photography` — hình chiếu phẳng RGB 3 kênh của bề mặt võng mạc), **SD-OCT / SS-OCT** sử dụng giao thoa ánh sáng cận hồng ngoại để thu nhận cấu trúc vi thể các lớp tế bào võng mạc theo chiều sâu trục quang học:

### 2.1. Phân cấp không gian dữ liệu OCT

| Cấp độ biểu diễn | Cấu trúc hình học | Kích thước điển hình | Ý nghĩa lâm sàng chính |
|---|---|---|---|
| **A-scan (1D)** | Hồ sơ phản xạ theo trục sâu `Z` | `512 – 1024` mẫu / tia | Phản ánh độ phản xạ qua 10 lớp giải phẫu võng mạc (ILM, RNFL, GCL, IPL, INL, OPL, ONL, EZ, RPE, Bruch's membrane). |
| **B-scan (2D Slice)** | Lát cắt ngang tạo bởi quét ngang nhiều A-scan (`Z x X`) | `496x512`, `512x1024` hoặc `1024x1024` (1 kênh Grayscale, 8-bit hoặc 16-bit) | Phát hiện dịch dưới võng mạc (`SRF`), dịch nội võng mạc (`IRF`), phù hoàng điểm đái tháo đường (`DME`), tân mạch hắc mạc (`CNV`), Drusen (`AMD`). |
| **C-scan / 3D Macular Cube** | Khối thể tích gồm `N` lát cắt B-scan song song (`N_slices x Z x X`) | `19 – 256` lát cắt B-scan (ví dụ `128 x 496 x 512` hoặc `64 x 512 x 1024`) | Định lượng thể tích dịch phù, bản đồ bề dày hoàng điểm (`ETDRS Thickness Map`), theo dõi đáp ứng tiêm nội nhãn Anti-VEGF. |

**Đặc điểm hình học cốt lõi cần lưu ý khi thiết kế pipeline AI**:
* **Tính bất đẳng hướng (Anisotropic Voxel Spacing)**: Độ phân giải theo chiều sâu trục `Z` rất cao (`~3.5 – 7 um/pixel`), chiều ngang `X` đạt `~10 – 15 um/pixel`, trong khi khoảng cách giữa hai lát B-scan liên tiếp theo trục `Y` thường thưa hơn nhiều (`~30 – 240 um/slice`). Nếu áp dụng tích chập 3D đẳng hướng (`3x3x3`) trực tiếp mà không xử lý khoảng cách trục `Y`, mô hình sẽ bị sai lệch hình thái liên lát cắt.
* **Nhiễu đốm giao thoa (Speckle Noise)**: Ảnh OCT chứa nhiễu nhân tính đặc trưng của giao thoa kế laser, không thể áp dụng trực tiếp bộ lọc Frangi kênh xanh lá như ảnh `FUNDUS`.

### 2.2. Đối chiếu 3 Định dạng Tệp Đầu vào (DICOM, Multi-page TIFF, PNG)

| Tiêu chí kỹ thuật | DICOM Ophthalmic Tomography | Multi-page TIFF (`.tif` / `.tiff`) | Nén Lossless PNG (Single B-scan / Stack) |
|---|---|---|---|
| **Chuẩn định danh** | `SOP Class UID 1.2.840.10008.5.1.4.1.1.77.1.5.4` | TIFF Revision 6.0 / BigTIFF (`IFD` đa trang) | ISO/IEC 15948 (`8-bit` hoặc `16-bit` Grayscale `L`/`I;16`) |
| **Dung lượng điển hình** | `25 MiB – 250 MiB` / volume | `15 MiB – 150 MiB` / stack | `200 KiB – 1.5 MiB` / lát cắt B-scan đơn |
| **Siêu dữ liệu hình học** | Đầy đủ `PixelSpacing`, `SliceThickness`, `FrameIncrementPointer`, `Laterality (L/R)` | Lưu qua TIFF Tags (`XResolution`, `ImageDescription`); thiếu chuẩn hóa y tế đồng nhất | Không mang voxel spacing chuẩn nếu không kèm file manifest JSON |
| **Rủi ro lộ lọt thông tin định danh (`PHI`)** | **Rất cao** — Header chứa `PatientName (0010,0010)`, `PatientID (0010,0020)`, `PatientBirthDate`, `InstitutionName` | **Trung bình** — Có thể chứa chuỗi văn bản thiết bị trong `ImageDescription` / EXIF | **Thấp** (ở mức header), nhưng cần kiểm tra chữ in đè (`burned-in text`) trên viền ảnh |
| **Thư viện giải mã Python** | `pydicom` (+ `pylibjpeg` nếu nén JPEG-LS / JPEG 2000) | `tifffile` hoặc `Pillow` (`ImageSequence.Iterator`) | `Pillow` (đã có sẵn trong `requirements.lock` của AI Core) |

---

## 3. Phân tích Mô hình AI Ứng viên & Dự toán Tài nguyên GPU / VRAM

### 3.1. So sánh Kiến trúc Mô hình: 2D/2.5D ResNet so với 3D U-Net

Để đáp ứng đồng thời hai bài toán lâm sàng (sàng lọc phân loại bất thường hoàng điểm và phân vùng dịch/lớp võng mạc), ba kiến trúc được đặt lên bàn cân:

1. **Phương án A — 2D / 2.5D ResNet-34 (hoặc ConvNeXt-Tiny) + FPN trên các lát cắt B-scan vùng trung tâm hoàng điểm (Foveal Stack)**:
   * **Cơ chế 2.5D**: Lấy `k = 3` hoặc `k = 5` lát cắt B-scan liên tiếp đi qua hố trung tâm hoàng điểm (`[z-2, z-1, z, z+1, z+2]`), xếp chồng làm các kênh đầu vào `[Batch, 5, 512, 512]`.
   * **Ưu điểm**: Tận dụng được trọng số tiền huấn luyện (`ImageNet` khởi tạo tầng đầu 5 kênh), giải quyết tốt tính bất đẳng hướng trục `Y`, suy luận rất nhanh trên GPU phổ thông và thậm chí chạy được ở tốc độ chấp nhận được (`~0.4 – 0.9s` / ca qua `ONNX Runtime`) trên CPU đa nhân khi không có GPU.
   * **Đầu ra**: Phân loại đa nhãn (`CNV`, `DME`, `Drusen`, `Normal`) kèm bản đồ nhiệt giải thích (`Grad-CAM`) hoặc mask phân vùng 2D cho lát cắt trung tâm.

2. **Phương án B — 2D U-Net (ResNet-34 Encoder) theo từng lát cắt B-scan + Tích hợp thể tích (Slice-wise 2D Segmentation -> Volume Aggregation)**:
   * **Cơ chế**: Phân vùng dịch (`IRF`, `SRF`, `PED`) độc lập hoặc theo cửa sổ `2.5D` trên từng lát B-scan trong khối `64 – 128` lát, sau đó nhân với `PixelSpacing * SliceThickness` để tính tổng thể tích dịch (`mm^3`).
   * **Ưu điểm**: Bộ nhớ VRAM cố định và nhỏ (xử lý theo mini-batch 8–16 lát B-scan cùng lúc), không bị tràn VRAM khi số lát cắt thay đổi theo hãng máy (`Heidelberg Spectralis` 49 lát vs `Zeiss Cirrus` 128 lát).

3. **Phương án C — 3D U-Net / Anisotropic 3D Residual U-Net trên toàn khối Volume**:
   * **Cơ chế**: Dùng tích chập 3D bất đẳng hướng (tầng nông dùng kernel `(1, 3, 3)`, tầng sâu dùng `(3, 3, 3)`) trên các patch 3D kích thước `32 x 256 x 256`.
   * **Nhược điểm kỹ thuật**: Chi phí bộ nhớ kích hoạt (`3D Activation Maps`) tăng bậc ba; suy luận toàn khối cần trượt cửa sổ 3D (`3D Sliding-Window Inference` có chồng lấp `overlap=0.5`), tốn nhiều thời gian và bắt buộc phải có GPU chuyên dụng.

### 3.2. Dự toán Ngân sách Tài nguyên Phần cứng (Analytical Compute & VRAM Budget)

Bảng dưới đây tính toán kích thước bộ nhớ lý thuyết (trọng số + activation tensors + optimizer states AdamW ở chế độ `AMP FP16`) cho kích thước lát cắt chuẩn `512 x 512` và khối `64 x 512 x 512`:

| Kiến trúc mô hình | Tham số (ước tính) | VRAM Suy luận (`FP16 / ONNX`, `batch=1`) | VRAM Huấn luyện (`AMP FP16`) | Độ trễ mục tiêu trên GPU (`NVIDIA T4 16GB` / `RTX 3060 12GB`) | Khả năng chạy CPU-only (`4 vCPU`, `4 GiB RAM`) |
|---|---:|---:|---:|---|---|
| **2.5D ResNet-34 Classifier** (`5 x 512 x 512`) | `~21.3M` | `~0.8 – 1.2 GiB` | `~4.5 – 6.5 GiB` (`batch=16`) | `< 40 ms` / ca | **Khả thi** (`~350 – 800 ms` với ONNX Runtime INT8/FP32) |
| **2.5D ResNet-34 U-Net** (`64` lát B-scan, mini-batch `8` lát) | `~24.4M` | `~1.8 – 2.5 GiB` | `~7.5 – 10.0 GiB` (`batch=8`) | `~0.4 – 0.9 s` / volume 64 lát | **Hạn chế** (`~12 – 25 s` / volume 64 lát -> chỉ chạy bất đồng bộ qua Worker) |
| **3D Anisotropic U-Net** (patch `32 x 256 x 256`, `base_ch=32`) | `~19.1M` | `~3.5 – 5.5 GiB` | `~12.0 – 16.0 GiB` (`batch=2`) | `~1.8 – 4.5 s` / volume (sliding window) | **Không khả thi cho vận hành thực tế** (nguy cơ nghẽn CPU > 60s & OOM container) |

**Kết luận hạ tầng phần cứng**:
* Để **huấn luyện** mô hình OCT ở Sprint 6: Cần tối thiểu **1 GPU có `>= 12 – 16 GiB VRAM`** (ví dụ `NVIDIA Tesla T4 16GB` trên Colab/Kaggle/Cloud hoặc máy trạm `RTX 3060 12GB` / `RTX 4070`), RAM hệ thống `>= 16 GiB`.
* Để **triển khai suy luận (Inference Deployment)** trong Docker Compose của nhóm (nơi máy developer có thể không có GPU NVIDIA rời): Phải đóng gói mô hình dưới dạng **ONNX Runtime** hỗ trợ dual-provider (`CUDAExecutionProvider` khi có GPU, tự động hạ về `CPUExecutionProvider` cho chế độ **2.5D Foveal B-scan Stack**).

---

## 4. Phương án Tiếp nhận Dữ liệu (Data Ingestion Architecture) & Bảo mật PHI

### 4.1. Tại sao KHÔNG truyền khối OCT qua JSON `image_base64` hiện tại?

Trong AI Contract `1.2.0` / `1.3.0`, ảnh `FUNDUS` (`~0.3 – 2.5 MiB`) được truyền trực tiếp qua trường `image_base64` với giới hạn cứng `maxBytes = 10 MiB` (`13,981,016` ký tự Base64). Với khối dữ liệu OCT (`25 – 250 MiB` DICOM/TIFF):
1. Mã hóa Base64 làm phình kích thước payload thêm `33%`, gây nghẽn băng thông HTTP nội bộ và tăng đột biến RAM cấp phát chuỗi JSON trên cả `.NET Worker (M2)` lẫn `FastAPI AI Core (M4)`.
2. File DICOM thô chứa `PHI` trực tiếp trong header nhị phân, vi phạm nguyên tắc **Zero-PHI trên đường truyền sang AI Core** (`patient_id: null`).

### 4.2. Kiến trúc Tiếp nhận Đề xuất cho Sprint 6 (Claim-Check + De-identification Gate)

```text
[Client / Máy chụp OCT]
       │  (1) Upload multipart/chunked lên Presigned Upload URL hoặc qua Analysis API
       ▼
[M2: Analysis Ingestion & DICOM Sanitizer]
       │  (2) Kiểm tra Magic Bytes (DICM / TIFF / PNG)
       │  (3) Tách & lưu metadata lâm sàng vào PostgreSQL; XÓA SẠCH toàn bộ DICOM PHI tags
       │      (0010,* Patient tags, 0008,0080 Institution, UIDs nhạy cảm), xuất gói chuẩn hóa
       │      (Cleaned Multi-frame / NPZ / Lossless TIFF) vào Private Object Storage (MinIO/S3)
       │  (4) Đẩy job bất đồng bộ vào hàng đợi riêng `aura.oct.jobs` (tách khỏi `aura.s3.jobs` của Fundus)
       ▼
[M2: OCT Worker] ──(RS256 Service JWT + X-Correlation-Id)──► [M4: AI Core /api/v2/analyze-oct]
       │                                                           │
       │  Payload chỉ truyền `storage_object_key` + `sha256`       │ (5) Stream trực tiếp từ Private Storage
       │  + `voxel_spacing_um` (KHÔNG truyền Base64 > 10 MiB)      │     Chạy 2.5D ONNX Inference
       ◄───────────────────────────────────────────────────────────┘
```

**Ưu điểm kiến trúc**:
* **Cô lập tải (Bulkhead Pattern)**: Hàng đợi `aura.oct.jobs` tách biệt hoàn toàn với luồng ảnh `FUNDUS`, đảm bảo một khối OCT nặng không bao giờ làm trễ SLA `< 1.8s` của luồng sàng lọc đáy mắt Frangi v0.2.
* **Tuân thủ ADR-004 (Private Storage) & Fail-Closed Auth**: Giữ nguyên xác thực `RS256 Service JWT` (`aud="aura-ai"`) và truy vết `X-Correlation-Id`.

---

## 5. Kết luận Phạm vi Triển khai Thực tế & Lộ trình Sprint 6

Dựa trên cân đối nguồn lực thực tế của nhóm dự án AURA (thời lượng Sprint 6, môi trường máy phát triển hỗn hợp CPU/GPU, và yêu cầu tính trung thực học thuật), **M4 kiến nghị phạm vi triển khai thực tế theo 2 mức rõ ràng**:

### 5.1. Quyết định Phạm vi (Scope Decision)

| Hạng mục | Quyết định cho Sprint 4–5 (Hiện tại) | Quyết định cho Sprint 6 (Đồ án tốt nghiệp) | Ngoài phạm vi Đồ án (Future Production Roadmap) |
|---|---|---|---|
| **Runtime `POST /api/v1/analyze`** | Chỉ nhận `FUNDUS`; từ chối `OCT` với `HTTP 422 UNSUPPORTED_MODALITY` | Giữ nguyên tương thích ngược `1.3.0` cho `FUNDUS` | Không phá vỡ v1 |
| **Định dạng OCT hỗ trợ** | Chưa mở parser OCT trong runtime | **Trong phạm vi (Phase 1)**: Lát cắt B-scan đơn (`Lossless PNG`) và chuỗi `Multi-page TIFF` vùng hoàng điểm (`3–5` lát B-scan, `<= 15 MiB`) đã khử định danh | Full Vendor Raw OCT (`.fda`, `.e2e`, `.img` độc quyền hãng máy) và khối DICOM 3D > 100 MiB thời gian thực |
| **Mô hình AI OCT** | Không kích hoạt | **Trong phạm vi (Phase 1)**: **2D / 2.5D ResNet-34 (ONNX Runtime)** phân loại 4 nhóm tổn thương B-scan (`CNV`, `DME`, `Drusen`, `Normal`) trên tập dữ liệu công khai có giấy phép rõ ràng (ví dụ *Kermany OCT2017 — CC BY 4.0*) kèm `Grad-CAM` overlay | Huấn luyện 3D U-Net đa trung tâm cần cụm GPU A100/V100 và thẩm định lâm sàng bệnh viện |
| **Cơ chế tích hợp M2/M3** | Giữ nguyên quy tắc Fundus | RFC riêng cho endpoint/mode OCT thử nghiệm (`is_experimental: true`) với timeout Worker `30s` | Quy trình PACS / HL7 FHIR Bệnh viện đầy đủ |

### 5.2. Kế hoạch Hành động Chi tiết cho Sprint 6

1. **Tuần đầu Sprint 6 — Xác minh Dữ liệu & Giấy phép (`M4`)**:
   * Tải tập con kiểm chứng từ nguồn mở giấy phép `CC BY 4.0` (ví dụ *Kermany et al., Labeled Optical Coherence Tomography (OCT) and Chest X-Ray Images for Classification*, Mendeley Data `CC BY 4.0`), lập `dataset_card_oct.md`, kiểm tra SHA-256 và chia split theo `patient_id` (`seed=42`, không rò rỉ bệnh nhân giữa train/val/test).
2. **Tuần giữa Sprint 6 — Huấn luyện & Xuất ONNX (`M4`)**:
   * Huấn luyện baseline **2D/2.5D ResNet-34** trên GPU (`AMP FP16`), xuất trọng số sang `ONNX` cố định phiên bản, đo confusion matrix, macro-F1, AUROC, cùng độ trễ CPU vs GPU.
3. **Tuần cuối Sprint 6 — Tích hợp Có Rào chắn (`M1 + M2 + M4`)**:
   * Mở parser `PNG / Multi-page TIFF` có kiểm tra giới hạn kích thước, nhiễu/độ tương phản B-scan và cờ `disclaimer` bắt buộc, đồng thời duy trì bài kiểm thử tải không ảnh hưởng luồng `FUNDUS`.

---

## 6. Tài liệu Tham khảo Học thuật & Tiêu chuẩn Kỹ thuật

1. NEMA PS3.3 — *DICOM Standard: Ophthalmic Tomography Image Information Object Definition* (`SOP Class UID 1.2.840.10008.5.1.4.1.1.77.1.5.4`).
2. Kermany, D. S. et al. (2018). *Identifying Medical Diagnoses and Treatable Diseases by Image-Based Deep Learning*. Cell, 172(5), 1122–1131 (`CC BY 4.0` OCT Dataset).
3. Bogunovic, H. et al. (2019). *RETOUCH: The Retinal OCT Fluid Detection and Segmentation Benchmark and Challenge*. IEEE Transactions on Medical Imaging, 38(8), 1858–1874.
4. Ronneberger, O., Fischer, P., & Brox, T. (2015). *U-Net: Convolutional Networks for Biomedical Image Segmentation*. MICCAI 2015.
5. Tài liệu nội bộ AURA: `docs/ai/feasibility_risk_model_and_oct_v1.0.md`, `docs/adr/ADR-004-private-storage.md`, `contracts/ai-openapi-1.3.yaml`.
